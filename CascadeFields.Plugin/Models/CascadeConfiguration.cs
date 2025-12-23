using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Microsoft.Xrm.Sdk;
using Newtonsoft.Json;

namespace CascadeFields.Plugin.Models
{
    /// <summary>
    /// Configuration for cascade field operations
    /// </summary>
    [DataContract]
    public class CascadeConfiguration
    {
        /// <summary>
        /// Unique identifier for this configuration
        /// </summary>
        [DataMember]
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>
        /// Name/description of this configuration
        /// </summary>
        [DataMember]
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>
        /// Parent entity logical name (the entity being monitored)
        /// </summary>
        [DataMember]
        [JsonProperty("parentEntity")]
        public string ParentEntity { get; set; }

        /// <summary>
        /// Related entity configurations (child records to update)
        /// </summary>
        [DataMember]
        [JsonProperty("relatedEntities")]
        public List<RelatedEntityConfig> RelatedEntities { get; set; }

        /// <summary>
        /// Whether this configuration is active
        /// </summary>
        [DataMember]
        [JsonProperty("isActive")]
        public bool IsActive { get; set; }

        public CascadeConfiguration()
        {
            RelatedEntities = new List<RelatedEntityConfig>();
            IsActive = true;
        }

        /// <summary>
        /// Validates the configuration
        /// </summary>
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
    /// Defines a field mapping between parent and child entities
    /// </summary>
    [DataContract]
    public class FieldMapping
    {
        /// <summary>
        /// Source field name on the parent entity
        /// </summary>
        [DataMember]
        [JsonProperty("sourceField")]
        public string SourceField { get; set; }

        /// <summary>
        /// Target field name on the child entity
        /// </summary>
        [DataMember]
        [JsonProperty("targetField")]
        public string TargetField { get; set; }

        /// <summary>
        /// Whether this field should trigger the cascade when changed
        /// </summary>
        [DataMember]
        [JsonProperty("isTriggerField")]
        public bool IsTriggerField { get; set; }

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(SourceField))
                throw new InvalidPluginExecutionException("SourceField is required in field mapping.");

            if (string.IsNullOrWhiteSpace(TargetField))
                throw new InvalidPluginExecutionException("TargetField is required in field mapping.");
        }
    }

    /// <summary>
    /// Configuration for a related entity to cascade updates to
    /// </summary>
    [DataContract]
    public class RelatedEntityConfig
    {
        /// <summary>
        /// Logical name of the related entity
        /// </summary>
        [DataMember]
        [JsonProperty("entityName")]
        public string EntityName { get; set; }

        /// <summary>
        /// Relationship name to use for querying related records
        /// </summary>
        [DataMember]
        [JsonProperty("relationshipName")]
        public string RelationshipName { get; set; }

        /// <summary>
        /// Filter criteria for selecting which child records to update (FetchXML format)
        /// </summary>
        [DataMember]
        [JsonProperty("filterCriteria")]
        public string FilterCriteria { get; set; }

        /// <summary>
        /// Whether to use the relationship name or direct lookup field
        /// </summary>
        [DataMember]
        [JsonProperty("useRelationship")]
        public bool UseRelationship { get; set; }

        /// <summary>
        /// Lookup field name if not using relationship (e.g., "parentaccountid")
        /// </summary>
        [DataMember]
        [JsonProperty("lookupFieldName")]
        public string LookupFieldName { get; set; }

        /// <summary>
        /// Field mappings specific to this related entity
        /// </summary>
        [DataMember]
        [JsonProperty("fieldMappings")]
        public List<FieldMapping> FieldMappings { get; set; }

        public RelatedEntityConfig()
        {
            UseRelationship = true;
            FieldMappings = new List<FieldMapping>();
        }

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
