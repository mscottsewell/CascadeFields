using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace CascadeFields.Configurator.Models.Domain
{
    /// <summary>
    /// Represents a complete cascade configuration for a parent entity
    /// Maps 1:1 with the JSON structure published to plugin steps
    /// </summary>
    public class CascadeConfigurationModel
    {
        [JsonProperty("id")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        [JsonProperty("parentEntity")]
        public string ParentEntity { get; set; } = string.Empty;

        [JsonProperty("isActive")]
        public bool IsActive { get; set; } = true;

        [JsonProperty("enableTracing")]
        public bool EnableTracing { get; set; } = true;

        [JsonProperty("relatedEntities")]
        public List<RelatedEntityConfigModel> RelatedEntities { get; set; } = new();

        /// <summary>
        /// Validates the configuration
        /// </summary>
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
        /// Converts this model to JSON string
        /// </summary>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        /// <summary>
        /// Creates a model from JSON string
        /// </summary>
        public static CascadeConfigurationModel FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentNullException(nameof(json));

            return JsonConvert.DeserializeObject<CascadeConfigurationModel>(json)
                ?? throw new InvalidOperationException("Failed to deserialize configuration JSON");
        }
    }

    /// <summary>
    /// Represents configuration for one child relationship
    /// </summary>
    public class RelatedEntityConfigModel
    {
        [JsonProperty("entityName")]
        public string EntityName { get; set; } = string.Empty;

        [JsonProperty("relationshipName")]
        public string? RelationshipName { get; set; }

        [JsonProperty("useRelationship")]
        public bool UseRelationship { get; set; } = true;

        [JsonProperty("lookupFieldName")]
        public string? LookupFieldName { get; set; }

        [JsonProperty("filterCriteria")]
        public string? FilterCriteria { get; set; }

        [JsonProperty("fieldMappings")]
        public List<FieldMappingModel> FieldMappings { get; set; } = new();

        /// <summary>
        /// Validates the related entity configuration
        /// </summary>
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
    /// Represents a single field mapping within a relationship
    /// </summary>
    public class FieldMappingModel
    {
        [JsonProperty("sourceField")]
        public string SourceField { get; set; } = string.Empty;

        [JsonProperty("targetField")]
        public string TargetField { get; set; } = string.Empty;

        [JsonProperty("isTriggerField")]
        public bool IsTriggerField { get; set; } = true;

        /// <summary>
        /// Validates the field mapping
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(SourceField))
                throw new InvalidOperationException("SourceField is required in field mapping.");

            if (string.IsNullOrWhiteSpace(TargetField))
                throw new InvalidOperationException("TargetField is required in field mapping.");
        }
    }

    /// <summary>
    /// Represents a single filter criterion
    /// </summary>
    public class FilterCriterionModel
    {
        public string Field { get; set; } = string.Empty;
        public string Operator { get; set; } = "eq";
        public string? Value { get; set; }

        /// <summary>
        /// Validates the filter criterion
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Field))
                throw new InvalidOperationException("Field is required in filter criterion.");

            if (string.IsNullOrWhiteSpace(Operator))
                throw new InvalidOperationException("Operator is required in filter criterion.");
        }

        /// <summary>
        /// Converts to the pipe-delimited filter string format
        /// </summary>
        public string ToFilterString()
        {
            return $"{Field}|{Operator}|{Value ?? string.Empty}";
        }

        /// <summary>
        /// Parses a filter string into a FilterCriterionModel
        /// </summary>
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
