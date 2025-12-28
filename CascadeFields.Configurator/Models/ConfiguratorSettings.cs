using System;
using System.Collections.Generic;
using System.Linq;

namespace CascadeFields.Configurator.Models
{
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

    public class SessionSettings
    {
        public string ConnectionKey { get; set; } = string.Empty;
        public string? SolutionUniqueName { get; set; }
        public Guid? SolutionId { get; set; }
        public string? ConfigurationJson { get; set; }
    }

    // Internal model for filter criteria - used within the control for UI binding
    public class SavedFilterCriteria
    {
        public string? Field { get; set; }
        public string? Operator { get; set; }
        public string? Value { get; set; }
    }
}
