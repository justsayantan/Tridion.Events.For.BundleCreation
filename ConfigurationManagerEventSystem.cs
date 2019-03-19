using System;
using System.Configuration;
using System.Reflection;
using System.Runtime.Caching;

namespace Tridion.Events.For.BundleCreation
{
    class ConfigurationManagerEventSystem
    {

        private const double CACHE_TIME_IN_MINUTES = 6000;

        #region public methods
        /// <summary>
        /// Returns the configuration value corresponding to the given key from Configuration corresponding to the current executing DLL AppSettings section
        /// </summary>
        public static string GetAppSetting(string key)
        {
            try
            {
                if (!Cache.Contains(key))
                {
                    KeyValueConfigurationElement configElement = DllConfiguration.AppSettings.Settings[key];

                    if (configElement != null)
                    {
                        string value = configElement.Value;
                        if (!string.IsNullOrEmpty(value))
                        {
                            StoreInCache(key, value);
                            return value;
                        }
                    }
                }
                else  // get from cache
                {
                    return Cache[key].ToString();
                }
            }
            catch (Exception ex)
            {
                // do nothing
            }

            return string.Empty;
        }

        /// <summary>
        /// Store any object in the cache 
        /// </summary>
        /// <param name="key">Identification of the item</param>
        /// <param name="item">The object to store (can be a page, component, schema, etc) </param>
        public static void StoreInCache(string key, object item)
        {
            CacheItemPolicy policy = new CacheItemPolicy();
            policy.AbsoluteExpiration = DateTime.Now.AddMinutes(CACHE_TIME_IN_MINUTES);
            policy.Priority = CacheItemPriority.Default;

            Cache.Add(key, item, policy);
        }
        #endregion

        #region private methods
        private static ObjectCache Cache
        {
            get
            {
                return MemoryCache.Default;
            }
        }

        /// <summary>
        /// Returns the Configuration object next to the current executing DLL
        /// </summary>
        private static System.Configuration.Configuration _dllConfiguration = null; 
        private static System.Configuration.Configuration DllConfiguration
        {
            get
            {
                if (_dllConfiguration == null)
                {
                    try
                    {
                        ExeConfigurationFileMap fileMap = new ExeConfigurationFileMap()
                        {
                            ExeConfigFilename = Assembly.GetExecutingAssembly().Location + ".config"
                        };
                        _dllConfiguration = System.Configuration.ConfigurationManager.OpenMappedExeConfiguration(fileMap, ConfigurationUserLevel.None);
                    }
                    catch (Exception ex)
                    {
                        // do nothing
                    }
                }
                return _dllConfiguration;
            }
        }
        #endregion
    }

}
