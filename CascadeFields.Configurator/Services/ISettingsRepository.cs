using System.Collections.Generic;
using System.Threading.Tasks;
using CascadeFields.Configurator.Models.Session;

namespace CascadeFields.Configurator.Services
{
    /// <summary>
    /// Interface for session persistence operations
    /// </summary>
    public interface ISettingsRepository
    {
        /// <summary>
        /// Loads session state for a specific connection
        /// </summary>
        /// <param name="connectionId">Unique identifier for the connection</param>
        Task<SessionState?> LoadSessionAsync(string connectionId);

        /// <summary>
        /// Saves session state for a specific connection
        /// </summary>
        /// <param name="session">Session state to save</param>
        Task SaveSessionAsync(SessionState session);

        /// <summary>
        /// Clears session state for a specific connection
        /// </summary>
        /// <param name="connectionId">Unique identifier for the connection</param>
        Task ClearSessionAsync(string connectionId);

        /// <summary>
        /// Gets all saved sessions
        /// </summary>
        Task<Dictionary<string, SessionState>> GetAllSessionsAsync();
    }
}
