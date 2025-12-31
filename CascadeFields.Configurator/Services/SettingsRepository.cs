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
    /// Handles persistence of session state to disk
    /// One session file per connection
    /// </summary>
    public class SettingsRepository : ISettingsRepository
    {
        private readonly string _settingsPath;

        /// <summary>
        /// Initializes the repository and ensures the settings folder exists.
        /// </summary>
        public SettingsRepository()
        {
            _settingsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "XrmToolBox",
                "Settings",
                "CascadeFields.Configurator");

            Directory.CreateDirectory(_settingsPath);
        }

        /// <summary>
        /// Loads the saved session for a specific connection ID, if present.
        /// </summary>
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

        /// <summary>
        /// Persists a session snapshot to disk for the given connection.
        /// </summary>
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

        /// <summary>
        /// Removes the persisted session file for a connection.
        /// </summary>
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

        /// <summary>
        /// Retrieves all saved sessions across connections, skipping invalid files.
        /// </summary>
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

        private string GetSessionFilePath(string connectionId)
        {
            // Sanitize connection ID for use as filename
            var safeConnectionId = string.Join("_",
                connectionId.Split(Path.GetInvalidFileNameChars()));

            return Path.Combine(_settingsPath, $"{safeConnectionId}.json");
        }
    }
}
