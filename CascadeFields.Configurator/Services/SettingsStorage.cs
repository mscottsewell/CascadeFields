using System;
using System.IO;
using System.Text;
using CascadeFields.Configurator.Models;
using Newtonsoft.Json;

namespace CascadeFields.Configurator.Services
{
    /// <summary>
    /// Lightweight static helper for persisting configurator-level settings (non-connection specific) to disk.
    /// </summary>
    internal static class SettingsStorage
    {
        private static readonly string AppFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CascadeFields.Configurator");
        private static readonly string SettingsFile = Path.Combine(AppFolder, "settings.json");

        /// <summary>
        /// Attempts to load persisted settings; returns null on first run or when deserialization fails.
        /// </summary>
        public static ConfiguratorSettings? Load()
        {
            try
            {
                if (!File.Exists(SettingsFile))
                {
                    return null;
                }

                var json = File.ReadAllText(SettingsFile, Encoding.UTF8);
                return JsonConvert.DeserializeObject<ConfiguratorSettings>(json);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Saves settings to disk, ignoring any IO errors because preferences are non-critical.
        /// </summary>
        public static void Save(ConfiguratorSettings settings)
        {
            try
            {
                Directory.CreateDirectory(AppFolder);
                var json = JsonConvert.SerializeObject(settings, Formatting.Indented);
                File.WriteAllText(SettingsFile, json, Encoding.UTF8);
            }
            catch
            {
                // Swallow persistence errors; settings are non-critical
            }
        }
    }
}
