using System;
using Microsoft.Xrm.Sdk.Metadata;
using CascadeConfigurationModel = CascadeFields.Plugin.Models.CascadeConfiguration;

namespace CascadeFields.Configurator.Models
{
    internal class SolutionItem
    {
        public Guid Id { get; set; }
        public string UniqueName { get; set; } = string.Empty;
        public string FriendlyName { get; set; } = string.Empty;

        public override string ToString() => FriendlyName;
    }

    internal class EntityItem
    {
        public string LogicalName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public EntityMetadata Metadata { get; set; } = default!;

        public override string ToString() => string.IsNullOrWhiteSpace(DisplayName)
            ? LogicalName
            : $"{DisplayName} ({LogicalName})";
    }

    internal class RelationshipItem
    {
        public string SchemaName { get; set; } = string.Empty;
        public string ReferencingEntity { get; set; } = string.Empty;
        public string ReferencingAttribute { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        
        // Property for ComboBox DisplayMember
        public string DisplayText => string.IsNullOrWhiteSpace(DisplayName)
            ? $"{ReferencingEntity} ({ReferencingAttribute})"
            : $"{DisplayName} ({ReferencingAttribute})";

        public override string ToString() => DisplayText;
    }

    internal class FormItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FormXml { get; set; } = string.Empty;

        public override string ToString() => Name;
    }

    internal class AttributeItem
    {
        public string LogicalName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public AttributeMetadata Metadata { get; set; } = default!;

        public override string ToString() => string.IsNullOrWhiteSpace(DisplayName)
            ? LogicalName
            : $"{DisplayName} ({LogicalName})";
    }

    internal class MappingRow
    {
        public string? SourceField { get; set; }
        public string? TargetField { get; set; }
        public bool IsTriggerField { get; set; } = true;
    }

    internal class ConfiguredRelationship
    {
        public string ParentEntity { get; set; } = string.Empty;
        public string ChildEntity { get; set; } = string.Empty;
        public string RelationshipName { get; set; } = string.Empty;
        public string DisplayName => $"{ParentEntity} → {ChildEntity} ({RelationshipName})";
        public CascadeConfigurationModel? Configuration { get; set; }
        public string? RawJson { get; set; }

        public override string ToString() => DisplayName;
    }
}
