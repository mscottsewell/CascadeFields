using System;
using System.Collections.Generic;
using System.Linq;

namespace CascadeFields.Configurator.Models
{
    /// <summary>
    /// Root settings model persisted between runs (per connection key).
    /// </summary>
    public class ConfiguratorSettings
    {
        public List<SessionSettings> Sessions { get; set; } = new();

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
    /// Persisted session payload for a single connection.
    /// </summary>
    public class SessionSettings
    {
        public string ConnectionKey { get; set; } = string.Empty;
        public string? SolutionUniqueName { get; set; }
        public Guid? SolutionId { get; set; }
        public string? ConfigurationJson { get; set; }
    }

    /// <summary>
    /// Internal filter shape used by grid controls when reading legacy filter strings.
    /// </summary>
    public class SavedFilterCriteria
    {
        public string? Field { get; set; }
        public string? Operator { get; set; }
        public string? Value { get; set; }
    }
}
