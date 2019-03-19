using System;
using System.Diagnostics;
using System.Xml;
using Tridion.ContentManager;
using Tridion.ContentManager.CommunicationManagement;
using Tridion.ContentManager.ContentManagement;
using Tridion.ContentManager.ContentManagement.Fields;
using Tridion.ContentManager.Extensibility;
using Tridion.ContentManager.Extensibility.Events;
using Tridion.ContentManager.Workflow;
using Tridion.Events.For.BundleCreation.Model;
using Tridion.Logging;

namespace Tridion.Events.For.BundleCreation
{
    [TcmExtension("CreateBundleAndAddItem")]
    public class CreateBundleAndAddItem : TcmExtension
    {
        #region public methods
        public CreateBundleAndAddItem()
        {
            Subscribe();
        }

        public void Subscribe()
        {
            EventSystem.Subscribe<Page, CheckInEventArgs>(OnPageCheckIn, EventPhases.TransactionCommitted);
        }
        #endregion

        #region private methods
        private void OnPageCheckIn(Page subject, CheckInEventArgs args, EventPhases phases)
        {
            Page page = (Page)subject.Session.GetObject(subject.Id);
            if (!IsPageInWorkflow(page))
            {
                var pubId = page.Id.ContextRepositoryId;
                Logger.Write($"Page Location: {page.OrganizationalItem.Title}", "Custom Event", LoggingCategory.General, TraceEventType.Information);

                // Get configurations from Tridion Config component
                string compId = $"tcm:{pubId}-{ConfigurationManagerEventSystem.GetAppSetting("SG-Bundle-Mapping-Configuration-Component")}-16";
                Logger.Write($"Config Component: {compId}", "Custom Event", LoggingCategory.General, TraceEventType.Information);
                Component conf_comp = (Component)subject.Session.GetObject(compId);
                Logger.Write($"conf_comp: {conf_comp.Title}", "Custom Event", LoggingCategory.General, TraceEventType.Information);

                SGBundleMappingConfiguration sgBundleMappingConfiguration = new SGBundleMappingConfiguration();
                sgBundleMappingConfiguration = ReadConfigurationCompoent(conf_comp,pubId.ToString());
                if (sgBundleMappingConfiguration.StructureGroupBundleSchemaMapping != null && sgBundleMappingConfiguration.StructureGroupBundleSchemaMapping.Count > 0)
                {
                    foreach (var item in sgBundleMappingConfiguration.StructureGroupBundleSchemaMapping)
                    {
                        if (item.StructGroupId.Contains(page.OrganizationalItem.Id.ItemId.ToString()))
                        {
                            CreateNewBundleAndStartWorkflow($"tcm:{pubId}-{item.BundleSchemaId}-8", page, $"tcm:{pubId}-{sgBundleMappingConfiguration.folderId}-2" , pubId.ToString());
                        }
                        else
                        {
                            Logger.Write($"No configuration found for the structure group of Page: {page.Id}", "Custom Event", LoggingCategory.General, TraceEventType.Information);
                        }
                    }
                }
                else
                {
                    Logger.Write($"No mapping found in the configuration component", "Custom Event", LoggingCategory.General, TraceEventType.Information);
                }
            }
        }

        /// <summary>
        /// Read the configuration Component and assign the velue into model
        /// </summary>
        /// <param name="conf_comp"> Configuration Component</param>
        /// <param name="pubId"> Publication Id </param>
        /// <returns>Object of SGBundleMappingConfiguration Model </returns>
        private SGBundleMappingConfiguration ReadConfigurationCompoent(Component conf_comp, string pubId)
        {
            SGBundleMappingConfiguration sgBundleMappingConfiguration = new SGBundleMappingConfiguration();
            BundleMapping bundleMapping = new BundleMapping();
            
            //Read Component Item Fields
            ItemFields fields = new ItemFields(conf_comp.Content, conf_comp.Schema);
            sgBundleMappingConfiguration.folderId = ((TextField)fields["folderId"]).Value;

            //Read Component Embedded Item Fields
            EmbeddedSchemaField embeddedField = (EmbeddedSchemaField)fields["structureGroupBundleSchemaMapping"];
            if (embeddedField != null && embeddedField.Values.Count > 0)
            {
                foreach(var item in embeddedField.Values)
                {
                    ItemFields embeddedFields = embeddedField.Value;
                    if (embeddedFields != null)
                    {
                        TextField structGroupId = (TextField)embeddedFields["structureGroupIds"];
                        TextField bundleSchemaId = (TextField)embeddedFields["bundleSchemaId"];
                        bundleMapping.StructGroupId = structGroupId.Value;
                        bundleMapping.BundleSchemaId = bundleSchemaId.Value;
                        sgBundleMappingConfiguration.StructureGroupBundleSchemaMapping.Add(bundleMapping);
                    }
                }                
            }
            return sgBundleMappingConfiguration;
        }

        /// <summary>
        /// Verify Whether the Page already In a Workflow
        /// </summary>
        /// <param name="page"></param>
        /// <returns></returns>
        private bool IsPageInWorkflow(Page page)
        {
            XmlElement xmlElement = page.ToXml();
            Logger.Write($"Inside : {xmlElement.InnerXml}", "Workflow", LoggingCategory.General, TraceEventType.Information);

            XmlNamespaceManager xmlNamespaceManager = new XmlNamespaceManager(new NameTable());
            xmlNamespaceManager.AddNamespace("tcm", "http://www.tridion.com/ContentManager/5.0");
            try
            {
                if (xmlElement.SelectSingleNode("//*[local-name()='ActivityInstance']", xmlNamespaceManager).Attributes["xlink:href"] != null && !string.IsNullOrEmpty(xmlElement.SelectSingleNode("//*[local-name()='ActivityInstance']", xmlNamespaceManager).Attributes["xlink:href"].Value) && xmlElement.SelectSingleNode("//*[local-name()='ActivityInstance']", xmlNamespaceManager).Attributes["xlink:href"].Value != "tcm:0-0-0")
                {
                    Logger.Write($"Inside if", "Workflow", LoggingCategory.General, TraceEventType.Information);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.Write($"Exception : {ex.Message}", "Workflow", LoggingCategory.General, TraceEventType.Information);
                return false;
            }
            return false;
        }

        /// <summary>
        /// Create The Bundle
        /// </summary>
        /// <param name="schemaId"></param>
        /// <param name="page"></param>
        /// <param name="bundleFolderUri"></param>
        /// <param name="publicationId"></param>
        private void CreateNewBundleAndStartWorkflow(string schemaId, Page page, string bundleFolderUri, string publicationId)
        {
            Session session = page.Session;            
            Logger.Write($"Bundle Schema Uri: {schemaId}", "Custom Event", LoggingCategory.General, TraceEventType.Information);
            TcmUri uri = new TcmUri(bundleFolderUri);
            Bundle bundle = new Bundle(session, uri);
            bundle.Title = "Bundle for " + page.Title;
            Schema bundleSchema = (Schema)session.GetObject(schemaId);
            bundle.MetadataSchema = bundleSchema;
            bundle.Save();            
            Logger.Write($"Bundle id: {bundle.Id}", "Custom Event", LoggingCategory.General, TraceEventType.Information);            
            AddItemIntoBundle(bundle, page);
            StartWorkflowForBundle(bundle, session);
        }

        /// <summary>
        /// Add Page and components into Bundle
        /// </summary>
        /// <param name="bundle"></param>
        /// <param name="page"></param>
        private void AddItemIntoBundle(Bundle bundle, Page page)
        {
            bundle.AddItem(page);
            foreach (ComponentPresentation cp in page.ComponentPresentations)
            {
                bundle.AddItem(cp.Component);
            }
            bundle.Save();
        }

        /// <summary>
        /// Start the workflow associated with bundle schema
        /// </summary>
        /// <param name="bundle"></param>
        /// <param name="session"></param>
        private void StartWorkflowForBundle(Bundle bundle, Session session)
        {
            StartWorkflowInstruction instruction = new StartWorkflowInstruction(session);
            instruction.Subjects.Add(bundle);
            ProcessInstance result = bundle.ContextRepository.StartWorkflow(instruction);
        }
        #endregion
    }
}