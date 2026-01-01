using System;
using System.Collections.Generic;
using Microsoft.Xrm.Sdk.Metadata;
using CascadeConfigurationModel = CascadeFields.Plugin.Models.CascadeConfiguration;

namespace CascadeFields.Configurator.Models.UI
{
    /// <summary>
    /// UI-friendly representation of a Dataverse solution record used for combo boxes and display.
    /// </summary>
    /// <remarks>
    /// Solutions in Dataverse are containers for customizations including plugin steps.
    /// When publishing cascade configurations, the plugin step is registered to the selected solution.
    /// This model simplifies solution selection in the configurator UI.
    /// </remarks>
    public class SolutionItem
    {
        /// <summary>
        /// Gets or sets the unique identifier (GUID) of the solution record.
        /// </summary>
        public Guid Id { get; set; }

        /// <summary>
        /// Gets or sets the unique name of the solution (e.g., "CascadeFields", "MyCustomSolution").
        /// This is the schema name used in code and deployments.
        /// </summary>
        public string UniqueName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the friendly display name of the solution shown in the UI.
        /// </summary>
        public string FriendlyName { get; set; } = string.Empty;

        /// <summary>
        /// Returns the friendly name for display in UI controls.
        /// </summary>
        /// <returns>The friendly display name.</returns>
        public override string ToString() => FriendlyName;
    }

    /// <summary>
    /// UI-friendly representation of a Dataverse entity including resolved metadata for display.
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b></para>
    /// Combines entity metadata with display-friendly formatting for use in combo boxes, lists, and grids.
    /// The <see cref="Metadata"/> property provides access to full entity metadata for advanced scenarios.
    ///
    /// <para><b>Usage:</b></para>
    /// Used throughout the configurator UI for entity selection (parent entity picker, child entity selection, etc.).
    /// The display format shows both friendly name and schema name for clarity: "Account (account)"
    /// </remarks>
    public class EntityItem
    {
        /// <summary>
        /// Gets or sets the logical (schema) name of the entity (e.g., "account", "contact", "opportunity").
        /// </summary>
        public string LogicalName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the friendly display name of the entity (e.g., "Account", "Contact", "Opportunity").
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the full entity metadata from Dataverse.
        /// Provides access to attributes, relationships, and other entity properties.
        /// </summary>
        public EntityMetadata Metadata { get; set; } = default!;

        /// <summary>
        /// Gets the display name with schema name in parentheses for UI controls.
        /// </summary>
        /// <remarks>
        /// Format: "Display Name (logicalname)" or just "logicalname" if no display name is available.
        /// Example: "Account (account)" or "new_customentity"
        /// </remarks>
        public string DisplayNameWithSchema => string.IsNullOrWhiteSpace(DisplayName)
            ? LogicalName
            : $"{DisplayName} ({LogicalName})";

        /// <summary>
        /// Returns the display name with schema for UI controls.
        /// </summary>
        /// <returns>The formatted display string.</returns>
        public override string ToString() => string.IsNullOrWhiteSpace(DisplayName)
            ? LogicalName
            : $"{DisplayName} ({LogicalName})";
    }

    /// <summary>
    /// Describes a Dataverse relationship option for selection in dialogs and configuration tabs.
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b></para>
    /// Represents a many-to-one or one-to-many relationship in Dataverse metadata.
    /// Used when selecting which child relationships to configure for cascade operations.
    ///
    /// <para><b>Relationship Types:</b></para>
    /// <list type="bullet">
    ///     <item><description><b>Many-to-One (N:1):</b> Child entity has lookup to parent (e.g., Contact.ParentCustomerId -> Account)</description></item>
    ///     <item><description><b>One-to-Many (1:N):</b> Parent entity to child entities (e.g., Account -> Contacts)</description></item>
    /// </list>
    ///
    /// <para><b>Usage:</b></para>
    /// Displayed in relationship picker dialogs and combo boxes to help users select
    /// which child relationships should receive cascaded field values from the parent entity.
    /// </remarks>
    public class RelationshipItem
    {
        /// <summary>
        /// Gets or sets the schema name of the relationship (e.g., "contact_customer_accounts").
        /// This is the unique identifier for the relationship in Dataverse metadata.
        /// </summary>
        public string SchemaName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the logical name of the referencing (child) entity.
        /// </summary>
        /// <remarks>
        /// For a Contact -> Account relationship, this would be "contact".
        /// </remarks>
        public string ReferencingEntity { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the logical name of the referencing (lookup) attribute on the child entity.
        /// </summary>
        /// <remarks>
        /// For a Contact -> Account relationship, this might be "parentcustomerid" or "accountid".
        /// </remarks>
        public string ReferencingAttribute { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the friendly display name of the relationship from metadata.
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the friendly display name of the child entity.
        /// </summary>
        public string ChildEntityDisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the friendly display name of the lookup field.
        /// </summary>
        public string LookupFieldDisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets the formatted display text for combo boxes and lists.
        /// </summary>
        /// <remarks>
        /// Format: "Display Name (lookupfield)" or "entity (lookupfield)" if no display name.
        /// Example: "Parent Account (parentaccountid)"
        /// </remarks>
        public string DisplayText => string.IsNullOrWhiteSpace(DisplayName)
            ? $"{ReferencingEntity} ({ReferencingAttribute})"
            : $"{DisplayName} ({ReferencingAttribute})";

        /// <summary>
        /// Returns the display text for UI controls.
        /// </summary>
        /// <returns>The formatted display string.</returns>
        public override string ToString() => DisplayText;
    }

    /// <summary>
    /// Describes a Dataverse attribute (field) and provides display helpers for drop-downs and grids.
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b></para>
    /// Represents a field on a Dataverse entity with display-friendly formatting.
    /// Used in field mapping grids, filter criteria builders, and attribute selection combo boxes.
    ///
    /// <para><b>Windows Forms Databinding:</b></para>
    /// The <see cref="FilterDisplayName"/> property must remain a property (not a method) for
    /// proper Windows Forms databinding. Using a method causes UI freezes in ComboBox/DataGridView controls.
    ///
    /// <para><b>Usage:</b></para>
    /// Used for selecting source/target fields in field mappings and filter fields in filter criteria.
    /// </remarks>
    public class AttributeItem
    {
        /// <summary>
        /// Gets or sets the logical (schema) name of the attribute (e.g., "territoryid", "statecode", "ownerid").
        /// </summary>
        public string LogicalName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the friendly display name of the attribute (e.g., "Territory", "Status", "Owner").
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the full attribute metadata from Dataverse.
        /// Provides access to attribute type, format, validation rules, and other properties.
        /// </summary>
        public AttributeMetadata Metadata { get; set; } = default!;

        /// <summary>
        /// Returns the display name with schema for UI controls.
        /// </summary>
        /// <returns>The formatted display string.</returns>
        public override string ToString() => string.IsNullOrWhiteSpace(DisplayName)
            ? LogicalName
            : $"{DisplayName} ({LogicalName})";

        /// <summary>
        /// Gets the formatted display name for filter and field selection controls.
        /// </summary>
        /// <remarks>
        /// <para><b>CRITICAL:</b></para>
        /// This MUST be a property, NOT a method, for Windows Forms databinding.
        /// Using a method causes UI freezes when bound to ComboBox/DataGridView controls.
        ///
        /// Format: "Display Name (logicalname)" or just "logicalname" if no display name.
        /// Example: "Territory (territoryid)" or "new_customfield"
        /// </remarks>
        public string FilterDisplayName => string.IsNullOrWhiteSpace(DisplayName)
            ? LogicalName
            : $"{DisplayName} ({LogicalName})";
    }

    /// <summary>
    /// Legacy mapping row structure used for Windows Forms designer-bound DataGridView controls.
    /// </summary>
    /// <remarks>
    /// This class provides a simplified data structure for binding field mappings to DataGridView controls
    /// in the Windows Forms designer. It's kept internal as it's an implementation detail of the grid binding.
    /// </remarks>
    internal class MappingRow
    {
        /// <summary>
        /// Gets or sets the source field logical name.
        /// </summary>
        public string? SourceField { get; set; }

        /// <summary>
        /// Gets or sets the target field logical name.
        /// </summary>
        public string? TargetField { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this field is a trigger field.
        /// </summary>
        public bool IsTriggerField { get; set; } = true;
    }

    /// <summary>
    /// Filter row structure used by Windows Forms filter grid databinding.
    /// </summary>
    /// <remarks>
    /// This class provides a simplified data structure for binding filter criteria to DataGridView controls.
    /// It's kept internal as it's an implementation detail of the filter grid control.
    /// </remarks>
    internal class FilterRow
    {
        /// <summary>
        /// Gets or sets the field logical name to filter on.
        /// </summary>
        public string? Field { get; set; }

        /// <summary>
        /// Gets or sets the filter operator code.
        /// </summary>
        public string? Operator { get; set; }

        /// <summary>
        /// Gets or sets the filter value.
        /// </summary>
        public string? Value { get; set; }
    }

    /// <summary>
    /// UI-friendly filter operator descriptor used by combo boxes and filter serialization.
    /// </summary>
    /// <remarks>
    /// Provides a mapping between operator codes (used in filter strings) and display text (shown in UI).
    /// </remarks>
    internal class FilterOperator
    {
        /// <summary>
        /// Gets or sets the operator code used in filter strings (e.g., "eq", "ne", "gt").
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the friendly display text shown in the UI (e.g., "Equal (=)", "Not Equal (!=)").
        /// </summary>
        public string Display { get; set; } = string.Empty;

        /// <summary>
        /// Returns the display text for UI controls.
        /// </summary>
        /// <returns>The friendly display text.</returns>
        public override string ToString() => Display;

        /// <summary>
        /// Gets the complete list of supported filter operators.
        /// </summary>
        /// <returns>A list of all available filter operators.</returns>
        /// <remarks>
        /// Supported operators:
        /// <list type="bullet">
        ///     <item><description>eq - Equal to</description></item>
        ///     <item><description>ne - Not equal to</description></item>
        ///     <item><description>gt - Greater than</description></item>
        ///     <item><description>lt - Less than</description></item>
        ///     <item><description>in - Value in comma-separated list</description></item>
        ///     <item><description>notin - Value not in list</description></item>
        ///     <item><description>null - Field is null</description></item>
        ///     <item><description>notnull - Field is not null</description></item>
        ///     <item><description>like - Pattern match with % wildcards</description></item>
        /// </list>
        /// </remarks>
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

    /// <summary>
    /// Represents a cascade configuration relationship that has already been published to Dataverse.
    /// Used for listing existing configurations and loading them for editing.
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b></para>
    /// When users load existing cascade configurations from Dataverse, this model represents
    /// each configured relationship with its associated configuration data.
    ///
    /// <para><b>Usage:</b></para>
    /// Displayed in configuration picker dialogs to allow users to select and edit existing
    /// cascade configurations. Contains both the relationship metadata and the full configuration JSON.
    /// </remarks>
    public class ConfiguredRelationship
    {
        /// <summary>
        /// Gets or sets the logical name of the parent entity.
        /// </summary>
        public string ParentEntity { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the logical name of the child entity.
        /// </summary>
        public string ChildEntity { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the friendly display name of the child entity.
        /// </summary>
        public string ChildEntityDisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the schema name of the relationship.
        /// </summary>
        public string RelationshipName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the friendly display name of the lookup field.
        /// </summary>
        public string LookupFieldDisplayName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the logical name of the lookup field.
        /// </summary>
        public string LookupFieldName { get; set; } = string.Empty;

        /// <summary>
        /// Gets the formatted display name for lists and combo boxes.
        /// </summary>
        /// <remarks>
        /// Format: "parent → child (relationshipname)"
        /// Example: "account → contact (contact_customer_accounts)"
        /// </remarks>
        public string DisplayName => $"{ParentEntity} → {ChildEntity} ({RelationshipName})";

        /// <summary>
        /// Gets or sets the full cascade configuration model loaded from Dataverse.
        /// Null if the configuration hasn't been loaded or failed to deserialize.
        /// </summary>
        public CascadeConfigurationModel? Configuration { get; set; }

        /// <summary>
        /// Gets or sets the raw JSON configuration string from the plugin step.
        /// Used for fallback display if deserialization fails.
        /// </summary>
        public string? RawJson { get; set; }

        /// <summary>
        /// Returns the display name for UI controls.
        /// </summary>
        /// <returns>The formatted display string.</returns>
        public override string ToString() => DisplayName;
    }
}
