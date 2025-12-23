using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk.Metadata;

namespace CascadeFields.Configurator
{
    public class SolutionOption
    {
        public Guid Id { get; set; }
        public string UniqueName { get; set; } = string.Empty;
        public string FriendlyName { get; set; } = string.Empty;
        public override string ToString() => FriendlyName;
    }

    public class EntityOption
    {
        public Guid MetadataId { get; set; }
        public string LogicalName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string DisplayLabel => string.IsNullOrWhiteSpace(DisplayName)
            ? LogicalName
            : $"{DisplayName} ({LogicalName})";
        public override string ToString() => string.IsNullOrWhiteSpace(DisplayName) ? LogicalName : DisplayName;
    }

    public class ViewOption
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string FetchXml { get; set; } = string.Empty;
        public override string ToString() => Name;
    }

    public class FormOption
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public HashSet<string> Fields { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public string DisplayLabel => string.IsNullOrWhiteSpace(Name) ? "Form" : Name;
        public override string ToString() => DisplayLabel;
    }

    public class LookupFieldOption
    {
        public string LogicalName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string[] Targets { get; set; } = Array.Empty<string>();
        public string DisplayLabel => string.IsNullOrWhiteSpace(DisplayName)
            ? LogicalName
            : $"{DisplayName} ({LogicalName})";
        public override string ToString() => DisplayName;
    }

    public class AttributeOption
    {
        public string LogicalName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public AttributeTypeCode? AttributeType { get; set; }
        public string Format { get; set; } = string.Empty;
        public string[] Targets { get; set; } = Array.Empty<string>();
        public string DisplayLabel => string.IsNullOrWhiteSpace(DisplayName)
            ? LogicalName
            : $"{DisplayName} ({LogicalName})";
        public override string ToString() => string.IsNullOrWhiteSpace(DisplayName) ? LogicalName : DisplayName;
    }

    public class MappingRowData
    {
        public AttributeOption ParentField { get; set; } = new AttributeOption();
        public AttributeOption ChildField { get; set; } = new AttributeOption();
        public bool IsTrigger { get; set; }
    }
}
