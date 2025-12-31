using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Microsoft.Xrm.Sdk;
using Newtonsoft.Json;

namespace CascadeFields.Plugin.Models
{
    /// <summary>
    /// Root configuration object that defines how field values cascade from a parent entity to related child entities.
    /// This configuration is serialized as JSON and stored in the plugin step's Unsecure Configuration field.
    /// </summary>
    /// <remarks>
    /// <para><b>Structure Overview:</b></para>
    /// A cascade configuration consists of:
    /// <list type="bullet">
    ///     <item><description>One parent entity that is monitored for changes</description></item>
    ///     <item><description>One or more related (child) entities that receive cascaded values</description></item>
    ///     <item><description>Field mappings that define which parent fields map to which child fields</description></item>
    ///     <item><description>Optional filter criteria to limit which child records are updated</description></item>
    ///     <item><description>Optional trigger fields that determine when cascades occur (vs. cascading on every parent update)</description></item>
    /// </list>
    ///
    /// <para><b>JSON Structure Example:</b></para>
    /// <code>
    /// {
    ///   "id": "account-to-contact",
    ///   "name": "Cascade Account fields to Contacts",
    ///   "parentEntity": "account",
    ///   "isActive": true,
    ///   "enableTracing": true,
    ///   "relatedEntities": [
    ///     {
    ///       "entityName": "contact",
    ///       "useRelationship": true,
    ///       "relationshipName": "contact_customer_accounts",
    ///       "filterCriteria": "&lt;filter&gt;&lt;condition attribute='statecode' operator='eq' value='0'/&gt;&lt;/filter&gt;",
    ///       "fieldMappings": [
    ///         {
    ///           "sourceField": "address1_city",
    ///           "targetField": "address1_city",
    ///           "isTriggerField": true
    ///         }
    ///       ]
    ///     }
    ///   ]
    /// }
    /// </code>
    ///
    /// <para><b>Validation:</b></para>
    /// Call the <see cref="Validate"/> method to ensure all required fields are present and properly configured.
    /// Validation is performed automatically by <see cref="Helpers.ConfigurationManager"/> when loading the configuration.
    /// </remarks>
    [DataContract]
    public class CascadeConfiguration
    {
        /// <summary>
        /// Gets or sets a unique identifier for this configuration.
        /// Used for identification and tracking purposes in logs and the configurator tool.
        /// </summary>
        /// <example>"account-to-contact-cascade"</example>
        [DataMember]
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets a descriptive name for this configuration.
        /// This name helps administrators understand what the configuration does.
        /// </summary>
        /// <example>"Cascade Account City to Contacts"</example>
        [DataMember]
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the logical name of the parent entity being monitored for changes.
        /// When this entity is updated, field values will be cascaded to related child entities.
        /// </summary>
        /// <example>"account", "contact", "opportunity"</example>
        [DataMember]
        [JsonProperty("parentEntity")]
        public string ParentEntity { get; set; }

        /// <summary>
        /// Gets or sets the list of related (child) entity configurations that define which entities receive cascaded values.
        /// Each related entity configuration includes the entity name, relationship, and field mappings.
        /// </summary>
        /// <remarks>
        /// At least one related entity configuration is required for the cascade to function.
        /// Multiple related entities can be configured to cascade values to different child entity types from the same parent.
        /// </remarks>
        [DataMember]
        [JsonProperty("relatedEntities")]
        public List<RelatedEntityConfig> RelatedEntities { get; set; }

        /// <summary>
        /// Gets or sets whether this configuration is active and should be processed by the plugin.
        /// Set to <c>false</c> to temporarily disable a configuration without deleting the plugin step.
        /// </summary>
        /// <remarks>
        /// Default value is <c>true</c>. When set to false, the plugin will skip processing even if triggered.
        /// </remarks>
        [DataMember]
        [JsonProperty("isActive")]
        public bool IsActive { get; set; }

        /// <summary>
        /// Gets or sets whether detailed tracing is enabled for diagnostic logging.
        /// When enabled, the plugin writes comprehensive trace information including field values, query details, and execution flow.
        /// </summary>
        /// <remarks>
        /// <para><b>Default:</b> <c>true</c></para>
        /// <para><b>Production Recommendation:</b> Set to <c>false</c> in production environments to reduce trace log verbosity and improve performance.</para>
        /// <para><b>Debugging:</b> Enable when troubleshooting issues or validating configuration behavior.</para>
        /// </remarks>
        [DataMember]
        [JsonProperty("enableTracing")]
        public bool EnableTracing { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CascadeConfiguration"/> class with default values.
        /// </summary>
        /// <remarks>
        /// Default values:
        /// <list type="bullet">
        ///     <item><description>RelatedEntities: Empty list</description></item>
        ///     <item><description>IsActive: true</description></item>
        ///     <item><description>EnableTracing: true</description></item>
        /// </list>
        /// </remarks>
        public CascadeConfiguration()
        {
            RelatedEntities = new List<RelatedEntityConfig>();
            IsActive = true;
            EnableTracing = true;
        }

        /// <summary>
        /// Validates the configuration to ensure all required fields are populated and relationships are properly defined.
        /// </summary>
        /// <exception cref="InvalidPluginExecutionException">
        /// Thrown when:
        /// <list type="bullet">
        ///     <item><description>ParentEntity is null, empty, or whitespace</description></item>
        ///     <item><description>RelatedEntities is null or contains no items</description></item>
        ///     <item><description>Any related entity configuration fails validation</description></item>
        /// </list>
        /// </exception>
        /// <remarks>
        /// This method performs recursive validation by calling <see cref="RelatedEntityConfig.Validate"/> on each related entity,
        /// which in turn validates all field mappings. Call this method before using the configuration to catch errors early.
        /// </remarks>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ParentEntity))
                throw new InvalidPluginExecutionException("ParentEntity is required in cascade configuration.");

            if (RelatedEntities == null || RelatedEntities.Count == 0)
                throw new InvalidPluginExecutionException("At least one related entity configuration is required.");

            foreach (var relatedEntity in RelatedEntities)
            {
                relatedEntity.Validate();
            }
        }
    }

    /// <summary>
    /// Defines a mapping between a field on the parent entity and a corresponding field on the child entity.
    /// Field mappings determine which values are copied from parent to child during cascade operations.
    /// </summary>
    /// <remarks>
    /// <para><b>Field Name Requirements:</b></para>
    /// Both SourceField and TargetField must use the logical names (schema names) of the fields, not display names.
    /// Example: Use "address1_city" not "Address 1: City"
    ///
    /// <para><b>Data Type Compatibility:</b></para>
    /// Source and target fields should have compatible data types. The plugin will attempt to copy values as-is,
    /// but incompatible types may cause runtime errors. String fields on the target will be truncated if the
    /// source value exceeds the target field's max length.
    ///
    /// <para><b>Trigger Fields:</b></para>
    /// When one or more field mappings are marked as trigger fields (IsTriggerField = true), the cascade only occurs
    /// when at least one of those trigger fields changes on the parent record. This optimizes performance by avoiding
    /// unnecessary cascade operations when unrelated fields are updated.
    /// If no trigger fields are specified, cascades occur on every parent update.
    /// </remarks>
    [DataContract]
    public class FieldMapping
    {
        /// <summary>
        /// Gets or sets the logical name of the field on the parent entity that provides the value to cascade.
        /// </summary>
        /// <example>"address1_city", "industrycode", "customfield_value"</example>
        /// <remarks>
        /// Must be a valid field (attribute) on the parent entity. Use the schema name, not the display name.
        /// </remarks>
        [DataMember]
        [JsonProperty("sourceField")]
        public string SourceField { get; set; }

        /// <summary>
        /// Gets or sets the logical name of the field on the child entity that receives the cascaded value.
        /// </summary>
        /// <example>"address1_city", "industrycode", "customfield_value"</example>
        /// <remarks>
        /// Must be a valid field (attribute) on the child entity. Use the schema name, not the display name.
        /// The target field should have a compatible data type with the source field.
        /// </remarks>
        [DataMember]
        [JsonProperty("targetField")]
        public string TargetField { get; set; }

        /// <summary>
        /// Gets or sets whether changes to this field on the parent entity should trigger the cascade operation.
        /// </summary>
        /// <remarks>
        /// <para><b>Trigger Field Behavior:</b></para>
        /// <list type="bullet">
        ///     <item><description>When set to <c>true</c>, changes to this source field will trigger the cascade to all related child records</description></item>
        ///     <item><description>When set to <c>false</c>, this field's value will be cascaded, but changes to it alone won't trigger the cascade</description></item>
        ///     <item><description>If ANY mapped field is marked as a trigger field, cascades only occur when at least one trigger field changes</description></item>
        ///     <item><description>If NO fields are marked as trigger fields, cascades occur on every parent update regardless of which fields changed</description></item>
        /// </list>
        /// <para><b>Performance Optimization:</b></para>
        /// Use trigger fields to reduce unnecessary cascade operations. For example, if only cascading address fields,
        /// mark them as trigger fields so cascades don't occur when unrelated fields like "description" are updated.
        /// </remarks>
        [DataMember]
        [JsonProperty("isTriggerField")]
        public bool IsTriggerField { get; set; }

        /// <summary>
        /// Validates the field mapping to ensure both source and target fields are specified.
        /// </summary>
        /// <exception cref="InvalidPluginExecutionException">
        /// Thrown when SourceField or TargetField is null, empty, or contains only whitespace.
        /// </exception>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(SourceField))
                throw new InvalidPluginExecutionException("SourceField is required in field mapping.");

            if (string.IsNullOrWhiteSpace(TargetField))
                throw new InvalidPluginExecutionException("TargetField is required in field mapping.");
        }
    }

    /// <summary>
    /// Configuration for a related (child) entity that receives cascaded field values from the parent entity.
    /// Defines the relationship between parent and child, field mappings, and optional filtering criteria.
    /// </summary>
    /// <remarks>
    /// <para><b>Relationship Configuration:</b></para>
    /// There are two ways to specify the relationship between parent and child:
    /// <list type="number">
    ///     <item>
    ///         <description><b>Using Relationship Name</b> (Recommended): Set UseRelationship=true and provide the RelationshipName.
    ///         This uses the metadata relationship defined in Dataverse (e.g., "contact_customer_accounts").
    ///         The Configurator tool can help you discover available relationships.</description>
    ///     </item>
    ///     <item>
    ///         <description><b>Using Lookup Field</b>: Set UseRelationship=false and provide the LookupFieldName.
    ///         This queries child records directly using the lookup field (e.g., "parentcustomerid").
    ///         Use this approach when the relationship name is unknown or when querying by lookup is more straightforward.</description>
    ///     </item>
    /// </list>
    ///
    /// <para><b>Filter Criteria:</b></para>
    /// Optional FetchXML filter conditions that limit which child records are updated. For example:
    /// <code>&lt;filter&gt;&lt;condition attribute='statecode' operator='eq' value='0'/&gt;&lt;/filter&gt;</code>
    /// This filters to only active (statecode=0) records. Leave empty to cascade to all related records.
    /// </remarks>
    [DataContract]
    public class RelatedEntityConfig
    {
        /// <summary>
        /// Gets or sets the logical name of the child entity that will receive cascaded field values.
        /// </summary>
        /// <example>"contact", "opportunity", "task"</example>
        /// <remarks>
        /// Must be a valid entity logical name in the Dataverse environment.
        /// </remarks>
        [DataMember]
        [JsonProperty("entityName")]
        public string EntityName { get; set; }

        /// <summary>
        /// Gets or sets the schema name of the relationship between the parent and child entities.
        /// Required when <see cref="UseRelationship"/> is <c>true</c>.
        /// </summary>
        /// <example>"contact_customer_accounts", "account_parent_account"</example>
        /// <remarks>
        /// <para>Use the relationship schema name, not the display name.</para>
        /// <para>The Configurator tool provides a picker to browse available relationships.</para>
        /// <para>You can find relationship names in the solution explorer or metadata browser.</para>
        /// </remarks>
        [DataMember]
        [JsonProperty("relationshipName")]
        public string RelationshipName { get; set; }

        /// <summary>
        /// Gets or sets optional FetchXML filter criteria to limit which child records are updated during cascade operations.
        /// </summary>
        /// <example>&lt;filter&gt;&lt;condition attribute='statecode' operator='eq' value='0'/&gt;&lt;/filter&gt;</example>
        /// <remarks>
        /// <para><b>Format:</b> Must be valid FetchXML filter syntax (without the outer &lt;fetch&gt; or &lt;entity&gt; tags).</para>
        /// <para><b>Use Cases:</b></para>
        /// <list type="bullet">
        ///     <item><description>Filter to only active records (statecode = 0)</description></item>
        ///     <item><description>Filter by record status, ownership, or any other field</description></item>
        ///     <item><description>Combine multiple conditions with AND/OR logic</description></item>
        /// </list>
        /// <para><b>Performance:</b> Filtering reduces the number of child records updated, improving performance.</para>
        /// <para>Leave empty or null to cascade to all related child records.</para>
        /// </remarks>
        [DataMember]
        [JsonProperty("filterCriteria")]
        public string FilterCriteria { get; set; }

        /// <summary>
        /// Gets or sets whether to use the relationship schema name or a direct lookup field to query related records.
        /// </summary>
        /// <value>
        /// <c>true</c> to use <see cref="RelationshipName"/>; <c>false</c> to use <see cref="LookupFieldName"/>.
        /// </value>
        /// <remarks>
        /// <para><b>Default:</b> <c>true</c> (use relationship name)</para>
        /// <para><b>When to use false:</b> Use lookup field mode when the relationship name is difficult to determine
        /// or when you prefer to query directly by the lookup field.</para>
        /// </remarks>
        [DataMember]
        [JsonProperty("useRelationship")]
        public bool UseRelationship { get; set; }

        /// <summary>
        /// Gets or sets the logical name of the lookup field on the child entity that references the parent entity.
        /// Required when <see cref="UseRelationship"/> is <c>false</c>.
        /// </summary>
        /// <example>"parentcustomerid", "accountid", "regardingobjectid"</example>
        /// <remarks>
        /// This is the schema name of the lookup (EntityReference) field on the child entity that points to the parent.
        /// Ignored when UseRelationship is true.
        /// </remarks>
        [DataMember]
        [JsonProperty("lookupFieldName")]
        public string LookupFieldName { get; set; }

        /// <summary>
        /// Gets or sets the list of field mappings that define which parent fields cascade to which child fields.
        /// At least one field mapping is required.
        /// </summary>
        /// <remarks>
        /// Each field mapping specifies a source field on the parent, a target field on the child, and optionally
        /// whether that field is a trigger field. See <see cref="FieldMapping"/> for details.
        /// </remarks>
        [DataMember]
        [JsonProperty("fieldMappings")]
        public List<FieldMapping> FieldMappings { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="RelatedEntityConfig"/> class with default values.
        /// </summary>
        /// <remarks>
        /// Default values:
        /// <list type="bullet">
        ///     <item><description>UseRelationship: true</description></item>
        ///     <item><description>FieldMappings: Empty list</description></item>
        /// </list>
        /// </remarks>
        public RelatedEntityConfig()
        {
            UseRelationship = true;
            FieldMappings = new List<FieldMapping>();
        }

        /// <summary>
        /// Validates the related entity configuration to ensure all required fields are populated correctly.
        /// </summary>
        /// <exception cref="InvalidPluginExecutionException">
        /// Thrown when:
        /// <list type="bullet">
        ///     <item><description>EntityName is null, empty, or whitespace</description></item>
        ///     <item><description>UseRelationship is true but RelationshipName is missing</description></item>
        ///     <item><description>UseRelationship is false but LookupFieldName is missing</description></item>
        ///     <item><description>FieldMappings is null or empty</description></item>
        ///     <item><description>Any field mapping fails validation</description></item>
        /// </list>
        /// </exception>
        /// <remarks>
        /// This method performs recursive validation by calling <see cref="FieldMapping.Validate"/> on each field mapping.
        /// </remarks>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(EntityName))
                throw new InvalidPluginExecutionException("EntityName is required in related entity configuration.");

            if (UseRelationship && string.IsNullOrWhiteSpace(RelationshipName))
                throw new InvalidPluginExecutionException("RelationshipName is required when UseRelationship is true.");

            if (!UseRelationship && string.IsNullOrWhiteSpace(LookupFieldName))
                throw new InvalidPluginExecutionException("LookupFieldName is required when UseRelationship is false.");

            if (FieldMappings == null || FieldMappings.Count == 0)
                throw new InvalidPluginExecutionException($"At least one field mapping is required for related entity '{EntityName}'.");

            foreach (var mapping in FieldMappings)
            {
                mapping.Validate();
            }
        }
    }
}
