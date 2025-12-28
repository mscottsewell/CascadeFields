using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk.Metadata;
using CascadeConfigurationModel = CascadeFields.Plugin.Models.CascadeConfiguration;

namespace CascadeFields.Configurator.Models.UI
{
    public class SolutionItem
    {
        public Guid Id { get; set; }
        public string UniqueName { get; set; } = string.Empty;
        public string FriendlyName { get; set; } = string.Empty;

        public override string ToString() => FriendlyName;
    }

    public class EntityItem
    {
        public string LogicalName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public EntityMetadata Metadata { get; set; } = default!;

        public override string ToString() => string.IsNullOrWhiteSpace(DisplayName)
            ? LogicalName
            : $"{DisplayName} ({LogicalName})";
    }

    public class RelationshipItem
    {
        public string SchemaName { get; set; } = string.Empty;
        public string ReferencingEntity { get; set; } = string.Empty;
        public string ReferencingAttribute { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        
        // Display names for formatting
        public string ChildEntityDisplayName { get; set; } = string.Empty;
        public string LookupFieldDisplayName { get; set; } = string.Empty;
        
        // Property for ComboBox DisplayMember
        public string DisplayText => string.IsNullOrWhiteSpace(DisplayName)
            ? $"{ReferencingEntity} ({ReferencingAttribute})"
            : $"{DisplayName} ({ReferencingAttribute})";

        public override string ToString() => DisplayText;
    }

    public class AttributeItem
    {
        public string LogicalName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public AttributeMetadata Metadata { get; set; } = default!;

        public override string ToString() => string.IsNullOrWhiteSpace(DisplayName)
            ? LogicalName
            : $"{DisplayName} ({LogicalName})";

        // CRITICAL: This must be a property, NOT a method, for Windows Forms databinding
        // Using a method here causes UI freezes when bound to ComboBox/DataGridView
        public string FilterDisplayName => string.IsNullOrWhiteSpace(DisplayName)
            ? LogicalName
            : $"{DisplayName} ({LogicalName})";
    }

    internal class MappingRow
    {
        public string? SourceField { get; set; }
        public string? TargetField { get; set; }
        public bool IsTriggerField { get; set; } = true;
    }

    internal class FilterRow
    {
        public string? Field { get; set; }
        public string? Operator { get; set; }
        public string? Value { get; set; }
    }

    internal class FilterOperator
    {
        public string Code { get; set; } = string.Empty;
        public string Display { get; set; } = string.Empty;

        public override string ToString() => Display;

        public static List<FilterOperator> GetAll() => new()
        {
            new() { Code = "eq", Display = "Equal (=)" },
            new() { Code = "ne", Display = "Not Equal (!=)" },
            new() { Code = "gt", Display = "Greater Than (>)" },
            new() { Code = "lt", Display = "Less Than (<)" },
            new() { Code = "in", Display = "In (value list)" },
            new() { Code = "notin", Display = "Not In" },
            new() { Code = "null", Display = "Is Null" },
            new() { Code = "notnull", Display = "Is Not Null" },
            new() { Code = "like", Display = "Like (pattern)" }
        };
    }

    public class ConfiguredRelationship
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
