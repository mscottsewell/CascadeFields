using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
namespace CascadeFields.Configurator
{
    [DataContract]
    public class ConfiguratorSettings
    {
        [DataMember]
        public Guid? SolutionId { get; set; }
        [DataMember]
        public string? SolutionUniqueName { get; set; }
        [DataMember]
        public string? ChildEntityLogicalName { get; set; }
        [DataMember]
        public Guid? TargetFormId { get; set; }
        [DataMember]
        public string? LookupFieldLogicalName { get; set; }
        [DataMember]
        public string? ParentEntityLogicalName { get; set; }
        [DataMember]
        public Guid? SourceFormId { get; set; }
        [DataMember]
        public string? AssemblyPath { get; set; }
        [DataMember]
        public List<FieldMappingSetting> LastMappings { get; set; } = new List<FieldMappingSetting>();

    }

    [DataContract]
    public class FieldMappingSetting
    {
        [DataMember]
        public string ParentField { get; set; } = string.Empty;
        [DataMember]
        public string ChildField { get; set; } = string.Empty;
        [DataMember]
        public bool IsTrigger { get; set; }
    }
}
