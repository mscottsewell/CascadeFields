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
        public string? ParentEntity { get; set; }
        public Guid? ParentFormId { get; set; }
        public string? ChildEntity { get; set; }
        public List<SavedFieldMapping>? FieldMappings { get; set; }
        public List<SavedFilterCriteria>? FilterCriteria { get; set; }
        public bool EnableTracing { get; set; } = true;
    }

    public class SavedFieldMapping
    {
        public string? SourceField { get; set; }
        public string? TargetField { get; set; }
        public bool IsTriggerField { get; set; }
    }

    public class SavedFilterCriteria
    {
        public string? Field { get; set; }
        public string? Operator { get; set; }
        public string? Value { get; set; }
    }
}
