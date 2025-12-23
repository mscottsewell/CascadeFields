using System;
using System.Collections.Generic;
namespace CascadeFields.Configurator
{
    public class ConfiguratorSettings
    {
        public Guid? SolutionId { get; set; }
        public string? SolutionUniqueName { get; set; }
        public string? ChildEntityLogicalName { get; set; }
        public Guid? TargetFormId { get; set; }
        public string? LookupFieldLogicalName { get; set; }
        public string? ParentEntityLogicalName { get; set; }
        public Guid? SourceFormId { get; set; }
        public string? AssemblyPath { get; set; }
        public List<FieldMappingSetting> LastMappings { get; set; } = new List<FieldMappingSetting>();

    }

    public class FieldMappingSetting
    {
        public string ParentField { get; set; } = string.Empty;
        public string ChildField { get; set; } = string.Empty;
        public bool IsTrigger { get; set; }
    }
}
