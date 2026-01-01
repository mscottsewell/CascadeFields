using System;
using System.IO;
using System.Text;
using CascadeFields.Configurator.Models;
using Newtonsoft.Json;

namespace CascadeFields.Configurator.Services
{
    /// <summary>
    /// Provides static utility methods for persisting application-wide configurator settings.
    /// </summary>
    /// <remarks>
    /// <para><strong>Purpose:</strong></para>
    /// <para>
    /// This class handles persistence of global configurator preferences that are NOT connection-specific.
    /// These settings apply across all Dataverse connections and persist between application sessions.
    /// </para>
    ///
    /// <para><strong>Storage Location:</strong></para>
    /// <para>
    /// Settings are stored in: %APPDATA%\CascadeFields.Configurator\settings.json
    /// </para>
    ///
    /// <para><strong>Difference from Session Storage:</strong></para>
    /// <list type="bullet">
    /// <item><description><see cref="SettingsStorage"/> - Global app settings (this class)</description></item>
    /// <item><description><see cref="ISettingsRepository"/> - Per-connection session state</description></item>
    /// </list>
    ///
    /// <para><strong>Error Handling:</strong></para>
    /// <para>
    /// All I/O errors are silently swallowed since user preferences are non-critical.
    /// The application should function normally even if settings cannot be persisted or loaded.
    /// </para>
    /// </remarks>
    internal static class SettingsStorage
    {
        private static readonly string AppFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CascadeFields.Configurator");
        private static readonly string SettingsFile = Path.Combine(AppFolder, "settings.json");

        /// <summary>
        /// Loads the persisted global configurator settings from disk.
        /// </summary>
        /// <returns>
        /// The deserialized settings object if successful, or null if the file doesn't exist,
        /// cannot be read, or contains invalid JSON.
        /// </returns>
        /// <remarks>
        /// This method will return null in the following scenarios:
        /// <list type="bullet">
        /// <item><description>First run (settings file doesn't exist yet)</description></item>
        /// <item><description>Settings file is corrupted or contains invalid JSON</description></item>
        /// <item><description>I/O error occurs while reading the file</description></item>
        /// </list>
        /// Callers should handle null by using default settings values.
        /// </remarks>
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
        /// Persists the global configurator settings to disk.
        /// </summary>
        /// <param name="settings">The settings object to save.</param>
        /// <remarks>
        /// <para>
        /// Saves the settings as indented JSON to %APPDATA%\CascadeFields.Configurator\settings.json.
        /// Creates the directory if it doesn't exist.
        /// </para>
        /// <para>
        /// All I/O errors are silently swallowed since settings persistence is non-critical.
        /// If the save fails, the user will simply start with default settings on next launch.
        /// </para>
        /// </remarks>
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
