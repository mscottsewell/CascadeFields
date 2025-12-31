using System;
using System.Collections.Generic;
using System.Linq;

namespace CascadeFields.Configurator.Models
{
    /// <summary>
    /// Root settings model persisted between runs (per connection key).
    /// Manages multiple session settings for different Dataverse connections.
    /// </summary>
    public class ConfiguratorSettings
    {
        /// <summary>
        /// Gets or sets the collection of session settings for all connections.
        /// </summary>
        public List<SessionSettings> Sessions { get; set; } = new();

        /// <summary>
        /// Retrieves an existing session for the specified connection key or creates a new one if none exists.
        /// </summary>
        /// <param name="connectionKey">The unique connection identifier.</param>
        /// <returns>The session settings for the connection.</returns>
        public SessionSettings GetOrCreateSession(string connectionKey)
        {
            var existing = Sessions.FirstOrDefault(s => string.Equals(s.ConnectionKey, connectionKey, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
            {
                return existing;
            }

            var session = new SessionSettings { ConnectionKey = connectionKey };
            Sessions.Add(session);
            return session;
        }
    }

    /// <summary>
    /// Persisted session payload for a single Dataverse connection.
    /// Stores the last selected solution and configuration JSON for session restoration.
    /// </summary>
    public class SessionSettings
    {
        /// <summary>
        /// Gets or sets the unique connection identifier.
        /// </summary>
        public string ConnectionKey { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the unique name of the last selected solution.
        /// </summary>
        public string? SolutionUniqueName { get; set; }

        /// <summary>
        /// Gets or sets the ID of the last selected solution.
        /// </summary>
        public Guid? SolutionId { get; set; }

        /// <summary>
        /// Gets or sets the last saved configuration JSON for this connection.
        /// </summary>
        public string? ConfigurationJson { get; set; }
    }

    /// <summary>
    /// Represents a saved filter criterion for backward compatibility with legacy filter string formats.
    /// Used internally when deserializing filter criteria from older configuration files.
    /// </summary>
    public class SavedFilterCriteria
    {
        /// <summary>
        /// Gets or sets the field name to filter on.
        /// </summary>
        public string? Field { get; set; }

        /// <summary>
        /// Gets or sets the filter operator (e.g., "eq", "ne", "gt", "lt").
        /// </summary>
        public string? Operator { get; set; }

        /// <summary>
        /// Gets or sets the filter value.
        /// </summary>
        public string? Value { get; set; }
    }
}
