using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tridion.Events.For.BundleCreation.Model
{
    public class SGBundleMappingConfiguration
    {
        public SGBundleMappingConfiguration()
        {
            StructureGroupBundleSchemaMapping = new List<BundleMapping>();
        }
        public string folderId { get; set; }
        public List<BundleMapping> StructureGroupBundleSchemaMapping { get; set; }
    }

    public class BundleMapping
    { 
        public string StructGroupId { get; set; }
        public string BundleSchemaId { get; set; }
    }
}
