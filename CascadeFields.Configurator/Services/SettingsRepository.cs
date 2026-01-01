using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CascadeFields.Configurator.Models.Session;
using Newtonsoft.Json;

namespace CascadeFields.Configurator.Services
{
    /// <summary>
    /// File-based implementation of session persistence using JSON serialization.
    /// </summary>
    /// <remarks>
    /// <para><strong>Storage Architecture:</strong></para>
    /// <list type="bullet">
    /// <item><description>One JSON file per connection in %APPDATA%\XrmToolBox\Settings\CascadeFields.Configurator</description></item>
    /// <item><description>Files are named using sanitized connection IDs</description></item>
    /// <item><description>JSON format allows for easy inspection and manual editing if needed</description></item>
    /// <item><description>Directory is created automatically on first use</description></item>
    /// </list>
    ///
    /// <para><strong>Error Handling Philosophy:</strong></para>
    /// <para>
    /// This class follows a "fail-soft" approach where all I/O errors are caught and logged
    /// but never propagated. Session persistence is a convenience feature, not a critical
    /// operation, so failures should not disrupt the user experience.
    /// </para>
    ///
    /// <para><strong>Thread Safety:</strong></para>
    /// <para>
    /// This implementation is not thread-safe. Concurrent access to the same session file
    /// from multiple instances could result in data corruption. In practice, this is not
    /// a concern since XrmToolBox typically runs single-instance per connection.
    /// </para>
    /// </remarks>
    public class SettingsRepository : ISettingsRepository
    {
        private readonly string _settingsPath;

        /// <summary>
        /// Initializes a new instance of the <see cref="SettingsRepository"/> class
        /// and ensures the settings directory exists.
        /// </summary>
        /// <remarks>
        /// Creates the settings directory at:
        /// %APPDATA%\XrmToolBox\Settings\CascadeFields.Configurator
        /// </remarks>
        public SettingsRepository()
        {
            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XrmToolBox",
                "Settings",
                "CascadeFields.Configurator");

            Directory.CreateDirectory(_settingsPath);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Returns null if the connection ID is invalid, the session file doesn't exist,
        /// or if deserialization fails. Logs errors to Debug output.
        /// </remarks>
        public async Task<SessionState?> LoadSessionAsync(string connectionId)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
                return null;

            var filePath = GetSessionFilePath(connectionId);

            if (!File.Exists(filePath))
                return null;

            try
            {
                var json = File.ReadAllText(filePath);
                return await Task.FromResult(JsonConvert.DeserializeObject<SessionState>(json));
            }
            catch (Exception ex)
            {
                // Log error but don't throw - session restore failure shouldn't break the app
                Debug.WriteLine($"Failed to load session for connection '{connectionId}': {ex.Message}");
                return null;
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Serializes the session to indented JSON for readability. Swallows I/O errors
        /// after logging them to Debug output.
        /// </remarks>
        /// <exception cref="ArgumentNullException">Thrown if session is null.</exception>
        /// <exception cref="ArgumentException">Thrown if session.ConnectionId is null or whitespace.</exception>
        public async Task SaveSessionAsync(SessionState session)
        {
            if (session == null)
                throw new ArgumentNullException(nameof(session));

            if (string.IsNullOrWhiteSpace(session.ConnectionId))
                throw new ArgumentException("ConnectionId is required", nameof(session));

            var filePath = GetSessionFilePath(session.ConnectionId);
            var json = JsonConvert.SerializeObject(session, Formatting.Indented);

            try
            {
                File.WriteAllText(filePath, json);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                // Log error but don't throw - session save failure shouldn't break the app
                Debug.WriteLine($"Failed to save session for connection '{session.ConnectionId}': {ex.Message}");
            }
        }

        /// <inheritdoc />
        /// <remarks>
        /// Silently succeeds if the session file doesn't exist. Logs deletion errors to Debug output.
        /// </remarks>
        public async Task ClearSessionAsync(string connectionId)
        {
            if (string.IsNullOrWhiteSpace(connectionId))
                return;

            var filePath = GetSessionFilePath(connectionId);

            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    // Log error but don't throw
                    Debug.WriteLine($"Failed to clear session for connection '{connectionId}': {ex.Message}");
                }
            }

            await Task.CompletedTask;
        }

        /// <inheritdoc />
        /// <remarks>
        /// Iterates through all .json files in the settings directory. Invalid or corrupted
        /// files are silently skipped. Returns an empty dictionary if the settings directory
        /// doesn't exist.
        /// </remarks>
        public async Task<Dictionary<string, SessionState>> GetAllSessionsAsync()
        {
            var sessions = new Dictionary<string, SessionState>();

            if (!Directory.Exists(_settingsPath))
                return sessions;

            foreach (var file in Directory.GetFiles(_settingsPath, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(file);
                    var session = JsonConvert.DeserializeObject<SessionState>(json);

                    if (session != null && !string.IsNullOrWhiteSpace(session.ConnectionId))
                    {
                        sessions[session.ConnectionId] = session;
                    }
                }
                catch
                {
                    // Skip invalid files
                }
            }

            return await Task.FromResult(sessions);
        }

        /// <summary>
        /// Constructs the full file path for a session file based on the connection ID.
        /// </summary>
        /// <param name="connectionId">The connection ID to convert to a filename.</param>
        /// <returns>The full path to the session file.</returns>
        /// <remarks>
        /// Sanitizes the connection ID by replacing invalid filename characters with underscores.
        /// This ensures the resulting filename is valid on all supported platforms.
        /// </remarks>
        private string GetSessionFilePath(string connectionId)
        {
            // Sanitize connection ID for use as filename
            var safeConnectionId = string.Join("_",
                connectionId.Split(Path.GetInvalidFileNameChars()));

            return Path.Combine(_settingsPath, $"{safeConnectionId}.json");
        }
    }
}
