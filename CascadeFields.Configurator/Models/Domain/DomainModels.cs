using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace CascadeFields.Configurator.Models.Domain
{
    /// <summary>
    /// Represents a complete cascade configuration for a parent entity.
    /// Maps 1:1 with the JSON structure published to plugin steps and stored in the Dataverse configuration.
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b></para>
    /// This is the root domain model that defines how field values cascade from a parent entity to one or more
    /// related child entities when the parent record is updated. The configuration is serialized to JSON and
    /// stored in the CascadeFields plugin step configuration.
    ///
    /// <para><b>Usage:</b></para>
    /// <list type="number">
    ///     <item><description>User creates configuration in the Configurator UI</description></item>
    ///     <item><description>UI serializes this model to JSON</description></item>
    ///     <item><description>JSON is stored in plugin step secure/unsecure configuration</description></item>
    ///     <item><description>Plugin deserializes JSON at runtime to execute cascades</description></item>
    /// </list>
    ///
    /// <para><b>Example JSON Structure:</b></para>
    /// <code>
    /// {
    ///   "id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
    ///   "name": "Account Territory Cascade",
    ///   "parentEntity": "account",
    ///   "isActive": true,
    ///   "enableTracing": true,
    ///   "relatedEntities": [
    ///     {
    ///       "entityName": "contact",
    ///       "relationshipName": "contact_customer_accounts",
    ///       "useRelationship": true,
    ///       "fieldMappings": [
    ///         {
    ///           "sourceField": "territoryid",
    ///           "targetField": "territoryid",
    ///           "isTriggerField": true
    ///         }
    ///       ]
    ///     }
    ///   ]
    /// }
    /// </code>
    /// </remarks>
    public class CascadeConfigurationModel
    {
        /// <summary>
        /// Gets or sets the unique identifier for this cascade configuration.
        /// Used to track and update configurations in the plugin repository.
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// Gets or sets the friendly name for this configuration.
        /// Used for display purposes in the UI and logging.
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the logical name of the parent entity (e.g., "account", "contact").
        /// This is the entity that triggers the cascade when updated.
        /// </summary>
        [JsonProperty("parentEntity")]
        public string ParentEntity { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether this configuration is active.
        /// When false, the plugin will not execute cascades for this configuration.
        /// </summary>
        [JsonProperty("isActive")]
        public bool IsActive { get; set; } = true;

        /// <summary>
        /// Gets or sets a value indicating whether detailed tracing should be enabled.
        /// When true, the plugin writes detailed execution logs for debugging and monitoring.
        /// </summary>
        [JsonProperty("enableTracing")]
        public bool EnableTracing { get; set; } = true;

        /// <summary>
        /// Gets or sets the collection of related entity configurations.
        /// Each entry defines how fields cascade to a specific child entity.
        /// </summary>
        [JsonProperty("relatedEntities")]
        public List<RelatedEntityConfigModel> RelatedEntities { get; set; } = new();

        /// <summary>
        /// Validates the configuration and all child relationships before publish/serialize.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the configuration is invalid (e.g., missing parent entity, no related entities, or invalid child configurations).
        /// </exception>
        /// <remarks>
        /// This method should be called before serializing the configuration to JSON or publishing to the plugin.
        /// It ensures all required fields are populated and all child configurations are valid.
        /// </remarks>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ParentEntity))
                throw new InvalidOperationException("ParentEntity is required in cascade configuration.");

            if (RelatedEntities == null || RelatedEntities.Count == 0)
                throw new InvalidOperationException("At least one related entity configuration is required.");

            foreach (var relatedEntity in RelatedEntities)
            {
                relatedEntity.Validate();
            }
        }

        /// <summary>
        /// Converts this model to indented JSON used by the configurator UI and plugin.
        /// </summary>
        /// <returns>A formatted JSON string representation of this configuration.</returns>
        /// <remarks>
        /// The returned JSON is formatted with indentation for readability.
        /// This format is used both for display in the UI and for storage in the plugin step configuration.
        /// </remarks>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        /// <summary>
        /// Parses a JSON payload into a cascade configuration model.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>A <see cref="CascadeConfigurationModel"/> instance populated from the JSON.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the json parameter is null or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown when JSON deserialization fails.</exception>
        /// <remarks>
        /// This method is used to load existing configurations from plugin steps or saved files.
        /// </remarks>
        public static CascadeConfigurationModel FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentNullException(nameof(json));

            return JsonConvert.DeserializeObject<CascadeConfigurationModel>(json)
                ?? throw new InvalidOperationException("Failed to deserialize configuration JSON");
        }
    }

    /// <summary>
    /// Represents configuration for one child relationship in a cascade configuration.
    /// Defines how a parent entity's fields cascade to a specific related child entity.
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b></para>
    /// Each RelatedEntityConfigModel defines one parent-to-child cascade relationship, including:
    /// <list type="bullet">
    ///     <item><description>How to find related child records (via relationship or lookup field)</description></item>
    ///     <item><description>Which fields to copy from parent to children (field mappings)</description></item>
    ///     <item><description>Optional filter criteria to limit which child records are updated</description></item>
    /// </list>
    ///
    /// <para><b>Relationship Resolution:</b></para>
    /// The plugin supports two methods to find child records:
    /// <list type="number">
    ///     <item><description><b>UseRelationship = true:</b> Uses the Dataverse relationship schema name to query related records (recommended)</description></item>
    ///     <item><description><b>UseRelationship = false:</b> Uses a direct lookup field name to query records (for manual queries or custom scenarios)</description></item>
    /// </list>
    ///
    /// <para><b>Example:</b></para>
    /// <code>
    /// var config = new RelatedEntityConfigModel
    /// {
    ///     EntityName = "contact",
    ///     RelationshipName = "contact_customer_accounts",
    ///     UseRelationship = true,
    ///     FilterCriteria = "statecode|eq|0",  // Only active contacts
    ///     FieldMappings = new List&lt;FieldMappingModel&gt;
    ///     {
    ///         new() { SourceField = "territoryid", TargetField = "territoryid", IsTriggerField = true }
    ///     }
    /// };
    /// </code>
    /// </remarks>
    public class RelatedEntityConfigModel
    {
        /// <summary>
        /// Gets or sets the logical name of the child entity (e.g., "contact", "opportunity").
        /// </summary>
        [JsonProperty("entityName")]
        public string EntityName { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the schema name of the relationship to use when querying child records.
        /// Required when <see cref="UseRelationship"/> is true.
        /// </summary>
        /// <remarks>
        /// Example relationship names: "contact_customer_accounts", "account_parent_account"
        /// </remarks>
        [JsonProperty("relationshipName")]
        public string? RelationshipName { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use the relationship schema name to find child records.
        /// When false, uses <see cref="LookupFieldName"/> instead.
        /// </summary>
        [JsonProperty("useRelationship")]
        public bool UseRelationship { get; set; } = true;

        /// <summary>
        /// Gets or sets the logical name of the lookup field to use when querying child records.
        /// Required when <see cref="UseRelationship"/> is false.
        /// </summary>
        /// <remarks>
        /// Example lookup field names: "parentaccountid", "customerid"
        /// </remarks>
        [JsonProperty("lookupFieldName")]
        public string? LookupFieldName { get; set; }

        /// <summary>
        /// Gets or sets an optional filter criteria string to limit which child records are updated.
        /// Format: "field|operator|value" (e.g., "statecode|eq|0" for active records only).
        /// </summary>
        /// <remarks>
        /// Multiple criteria can be separated by semicolons.
        /// Common operators: eq, ne, gt, lt, in, notin, null, notnull, like
        /// </remarks>
        [JsonProperty("filterCriteria")]
        public string? FilterCriteria { get; set; }

        /// <summary>
        /// Gets or sets the collection of field mappings that define which fields cascade from parent to child.
        /// At least one mapping is required.
        /// </summary>
        [JsonProperty("fieldMappings")]
        public List<FieldMappingModel> FieldMappings { get; set; } = new();

        /// <summary>
        /// Validates the related entity configuration to ensure relationship/lookup consistency and mappings exist.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when configuration is invalid (e.g., missing entity name, invalid relationship/lookup setup, or no field mappings).
        /// </exception>
        /// <remarks>
        /// Validation rules:
        /// <list type="bullet">
        ///     <item><description>EntityName must be specified</description></item>
        ///     <item><description>If UseRelationship is true, RelationshipName must be specified</description></item>
        ///     <item><description>If UseRelationship is false, LookupFieldName must be specified</description></item>
        ///     <item><description>At least one field mapping must exist</description></item>
        ///     <item><description>All field mappings must be valid</description></item>
        /// </list>
        /// </remarks>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(EntityName))
                throw new InvalidOperationException("EntityName is required in related entity configuration.");

            if (UseRelationship && string.IsNullOrWhiteSpace(RelationshipName))
                throw new InvalidOperationException("RelationshipName is required when UseRelationship is true.");

            if (!UseRelationship && string.IsNullOrWhiteSpace(LookupFieldName))
                throw new InvalidOperationException("LookupFieldName is required when UseRelationship is false.");

            if (FieldMappings == null || FieldMappings.Count == 0)
                throw new InvalidOperationException($"At least one field mapping is required for entity '{EntityName}'.");

            foreach (var mapping in FieldMappings)
            {
                mapping.Validate();
            }
        }
    }

    /// <summary>
    /// Represents a single field mapping within a relationship, defining how a field value cascades from parent to child.
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b></para>
    /// A field mapping specifies that when the source field on the parent entity changes,
    /// its value should be copied to the target field on all related child entities.
    ///
    /// <para><b>Trigger Fields:</b></para>
    /// When <see cref="IsTriggerField"/> is true, the cascade only executes when this specific field changes on the parent.
    /// This optimizes plugin performance by only running cascades when relevant fields are updated.
    /// When false, this field is always updated when any trigger field in the configuration changes.
    ///
    /// <para><b>Example Use Cases:</b></para>
    /// <list type="bullet">
    ///     <item><description>Copy account territory to all related contacts when territory changes</description></item>
    ///     <item><description>Cascade status changes from parent to child records</description></item>
    ///     <item><description>Sync pricing tier from account to all opportunities</description></item>
    /// </list>
    ///
    /// <para><b>Example:</b></para>
    /// <code>
    /// var mapping = new FieldMappingModel
    /// {
    ///     SourceField = "territoryid",      // Parent account's territory
    ///     TargetField = "territoryid",      // Child contact's territory
    ///     IsTriggerField = true            // Only cascade when parent territory changes
    /// };
    /// </code>
    /// </remarks>
    public class FieldMappingModel
    {
        /// <summary>
        /// Gets or sets the logical name of the field on the parent entity to read from.
        /// </summary>
        /// <remarks>
        /// This is the field whose value will be copied to child records.
        /// Example: "territoryid", "statecode", "ownerid"
        /// </remarks>
        [JsonProperty("sourceField")]
        public string SourceField { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the logical name of the field on the child entity to write to.
        /// </summary>
        /// <remarks>
        /// This is the field that will receive the value from the parent.
        /// Must be compatible with the source field type.
        /// Example: "territoryid", "statecode", "ownerid"
        /// </remarks>
        [JsonProperty("targetField")]
        public string TargetField { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets a value indicating whether changes to this field should trigger the cascade.
        /// </summary>
        /// <remarks>
        /// <para><b>When true:</b></para>
        /// The plugin only executes when this specific field changes on the parent record.
        /// This optimizes performance by avoiding unnecessary cascade operations.
        ///
        /// <para><b>When false:</b></para>
        /// This field is always updated when any trigger field in the configuration changes,
        /// but changes to this field alone won't trigger the cascade.
        ///
        /// <para><b>Best Practice:</b></para>
        /// Set to true for fields that should independently trigger cascades (e.g., territory).
        /// Set to false for fields that should be updated along with other changes (e.g., last modified date).
        /// </remarks>
        [JsonProperty("isTriggerField")]
        public bool IsTriggerField { get; set; } = true;

        /// <summary>
        /// Validates that a field mapping includes both source and target fields.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when either SourceField or TargetField is null or empty.
        /// </exception>
        /// <remarks>
        /// Both source and target fields are required for the plugin to execute the field copy operation.
        /// </remarks>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(SourceField))
                throw new InvalidOperationException("SourceField is required in field mapping.");

            if (string.IsNullOrWhiteSpace(TargetField))
                throw new InvalidOperationException("TargetField is required in field mapping.");
        }
    }

    /// <summary>
    /// Represents a single filter criterion used to limit which child records are updated during cascades.
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b></para>
    /// Filter criteria allow selective cascade updates by filtering which child records receive the cascaded values.
    /// For example, you might only want to update active contacts, or contacts in a specific region.
    ///
    /// <para><b>Format:</b></para>
    /// Filters use a pipe-delimited format: "field|operator|value"
    /// <list type="bullet">
    ///     <item><description><b>field:</b> The logical name of the child entity field to filter on</description></item>
    ///     <item><description><b>operator:</b> Comparison operator (eq, ne, gt, lt, in, notin, null, notnull, like)</description></item>
    ///     <item><description><b>value:</b> The value to compare against (optional for null/notnull operators)</description></item>
    /// </list>
    ///
    /// <para><b>Examples:</b></para>
    /// <code>
    /// // Only update active contacts
    /// var filter1 = new FilterCriterionModel { Field = "statecode", Operator = "eq", Value = "0" };
    ///
    /// // Only update contacts without a territory
    /// var filter2 = new FilterCriterionModel { Field = "territoryid", Operator = "null" };
    ///
    /// // Only update contacts in specific regions
    /// var filter3 = new FilterCriterionModel { Field = "address1_stateorprovince", Operator = "in", Value = "CA,NY,TX" };
    /// </code>
    /// </remarks>
    public class FilterCriterionModel
    {
        /// <summary>
        /// Gets or sets the logical name of the child entity field to filter on.
        /// </summary>
        public string Field { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the comparison operator for the filter.
        /// </summary>
        /// <remarks>
        /// Supported operators:
        /// <list type="bullet">
        ///     <item><description><b>eq:</b> Equal to</description></item>
        ///     <item><description><b>ne:</b> Not equal to</description></item>
        ///     <item><description><b>gt:</b> Greater than</description></item>
        ///     <item><description><b>lt:</b> Less than</description></item>
        ///     <item><description><b>in:</b> Value is in a comma-separated list</description></item>
        ///     <item><description><b>notin:</b> Value is not in a comma-separated list</description></item>
        ///     <item><description><b>null:</b> Field is null</description></item>
        ///     <item><description><b>notnull:</b> Field is not null</description></item>
        ///     <item><description><b>like:</b> Pattern match (use % wildcards)</description></item>
        /// </list>
        /// </remarks>
        public string Operator { get; set; } = "eq";

        /// <summary>
        /// Gets or sets the value to compare against.
        /// Optional for null/notnull operators.
        /// </summary>
        public string? Value { get; set; }

        /// <summary>
        /// Validates the filter criterion has the required pieces before serialization.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when either Field or Operator is null or empty.
        /// </exception>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Field))
                throw new InvalidOperationException("Field is required in filter criterion.");

            if (string.IsNullOrWhiteSpace(Operator))
                throw new InvalidOperationException("Operator is required in filter criterion.");
        }

        /// <summary>
        /// Converts to the pipe-delimited filter string format consumed by the plugin.
        /// </summary>
        /// <returns>A pipe-delimited string in the format "field|operator|value".</returns>
        /// <remarks>
        /// This format is used internally by the plugin to parse filter criteria from the configuration JSON.
        /// Example output: "statecode|eq|0" or "territoryid|null|"
        /// </remarks>
        public string ToFilterString()
        {
            return $"{Field}|{Operator}|{Value ?? string.Empty}";
        }

        /// <summary>
        /// Parses a pipe-delimited filter string into a FilterCriterionModel instance.
        /// </summary>
        /// <param name="filterString">The pipe-delimited filter string to parse.</param>
        /// <returns>
        /// A <see cref="FilterCriterionModel"/> instance if parsing succeeds; otherwise, null.
        /// </returns>
        /// <remarks>
        /// Expected format: "field|operator|value"
        /// The value component is optional. If the input string is invalid or null, returns null.
        /// </remarks>
        public static FilterCriterionModel? FromFilterString(string filterString)
        {
            if (string.IsNullOrWhiteSpace(filterString))
                return null;

            var parts = filterString.Split('|');
            if (parts.Length < 2)
                return null;

            return new FilterCriterionModel
            {
                Field = parts[0],
                Operator = parts[1],
                Value = parts.Length > 2 ? parts[2] : null
            };
        }
    }
}
