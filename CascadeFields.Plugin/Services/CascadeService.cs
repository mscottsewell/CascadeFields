using CascadeFields.Plugin.Helpers;
using CascadeFields.Plugin.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModelCascadeConfiguration = CascadeFields.Plugin.Models.CascadeConfiguration;

namespace CascadeFields.Plugin.Services
{
    /// <summary>
    /// Service for executing cascade field operations
    /// </summary>
    public class CascadeService
    {
        private readonly IOrganizationService _service;
        private readonly PluginTracer _tracer;
        private readonly Dictionary<string, AttributeMetadata> _attributeMetadataCache;

        public CascadeService(IOrganizationService service, PluginTracer tracer)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
            _attributeMetadataCache = new Dictionary<string, AttributeMetadata>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Checks if any trigger fields have changed
        /// </summary>
        public bool HasTriggerFieldChanged(Entity target, Entity preImage, ModelCascadeConfiguration config)
        {
            _tracer.StartOperation("HasTriggerFieldChanged");

            // Collect all unique trigger fields from all related entities
            var triggerFields = new HashSet<string>();
            foreach (var relatedEntity in config.RelatedEntities)
            {
                if (relatedEntity.FieldMappings != null)
                {
                    foreach (var mapping in relatedEntity.FieldMappings.Where(m => m.IsTriggerField))
                    {
                        triggerFields.Add(mapping.SourceField);
                    }
                }
            }

            if (triggerFields.Count == 0)
            {
                _tracer.Info("No trigger fields configured, will process all field changes");
                _tracer.EndOperation("HasTriggerFieldChanged");
                return true;
            }

            _tracer.Info($"Checking {triggerFields.Count} trigger fields");

            foreach (var field in triggerFields)
            {
                if (target.Contains(field))
                {
                    // Check if value actually changed
                    if (preImage != null && preImage.Contains(field))
                    {
                        var oldValue = preImage[field];
                        var newValue = target[field];

                        if (!AreValuesEqual(oldValue, newValue))
                        {
                            _tracer.Info($"Trigger field '{field}' changed from '{oldValue}' to '{newValue}'");
                            _tracer.EndOperation("HasTriggerFieldChanged");
                            return true;
                        }
                    }
                    else
                    {
                        _tracer.Info($"Trigger field '{field}' is present in target (new value)");
                        _tracer.EndOperation("HasTriggerFieldChanged");
                        return true;
                    }
                }
            }

            _tracer.Info("No trigger fields changed");
            _tracer.EndOperation("HasTriggerFieldChanged");
            return false;
        }

        /// <summary>
        /// Cascades field values to related entities
        /// </summary>
        public void CascadeFieldValues(Entity target, Entity preImage, ModelCascadeConfiguration config)
        {
            _tracer.StartOperation("CascadeFieldValues");

            try
            {
                // Process each related entity configuration
                foreach (var relatedEntityConfig in config.RelatedEntities)
                {
                    // Get values to cascade specific to this related entity
                    var valuesToCascade = GetValuesToCascade(target, preImage, relatedEntityConfig);

                    if (valuesToCascade.Count == 0)
                    {
                        _tracer.Info($"No values to cascade for {relatedEntityConfig.EntityName}");
                        continue;
                    }

                    _tracer.Info($"Cascading {valuesToCascade.Count} field values to {relatedEntityConfig.EntityName}");
                    CascadeToRelatedEntity(target.Id, valuesToCascade, relatedEntityConfig, config);
                }

                _tracer.EndOperation("CascadeFieldValues");
            }
            catch (Exception ex)
            {
                _tracer.Error("Error in CascadeFieldValues", ex);
                throw;
            }
        }

        private Dictionary<string, object> GetValuesToCascade(Entity target, Entity preImage, RelatedEntityConfig relatedEntityConfig)
        {
            var values = new Dictionary<string, object>();

            foreach (var mapping in relatedEntityConfig.FieldMappings)
            {
                // Prefer target over preImage
                if (target.Contains(mapping.SourceField))
                {
                    var mappedValue = GetMappedValueForTarget(target, mapping, relatedEntityConfig.EntityName);
                    values[mapping.TargetField] = mappedValue;
                    _tracer.Debug($"Mapping: {mapping.SourceField} -> {mapping.TargetField} = {mappedValue}");
                }
                else if (preImage != null && preImage.Contains(mapping.SourceField))
                {
                    var mappedValue = GetMappedValueForTarget(preImage, mapping, relatedEntityConfig.EntityName);
                    values[mapping.TargetField] = mappedValue;
                    _tracer.Debug($"Mapping (from preImage): {mapping.SourceField} -> {mapping.TargetField} = {mappedValue}");
                }
            }

            return values;
        }

        private object GetMappedValueForTarget(Entity sourceEntity, FieldMapping mapping, string targetEntityName)
        {
            var mappedValue = sourceEntity[mapping.SourceField];
            var formattedValue = sourceEntity.FormattedValues != null && sourceEntity.FormattedValues.Contains(mapping.SourceField)
                ? sourceEntity.FormattedValues[mapping.SourceField]
                : null;

            var targetAttributeMetadata = GetTargetAttributeMetadata(targetEntityName, mapping.TargetField);
            var targetAttributeType = targetAttributeMetadata?.AttributeType;

            if (targetAttributeType.HasValue && (targetAttributeType.Value == AttributeTypeCode.String || targetAttributeType.Value == AttributeTypeCode.Memo))
            {
                var textValue = ConvertToTextValue(mappedValue, formattedValue);
                if (textValue == null)
                {
                    return null;
                }

                return ApplyTruncationIfNeeded(textValue, targetAttributeMetadata, mapping.TargetField);
            }

            return mappedValue;
        }

        private AttributeMetadata GetTargetAttributeMetadata(string entityLogicalName, string attributeLogicalName)
        {
            if (string.IsNullOrWhiteSpace(entityLogicalName) || string.IsNullOrWhiteSpace(attributeLogicalName))
            {
                return null;
            }

            var cacheKey = $"{entityLogicalName}:{attributeLogicalName}";

            if (_attributeMetadataCache.TryGetValue(cacheKey, out var cachedMetadata))
            {
                return cachedMetadata;
            }

            try
            {
                var request = new RetrieveAttributeRequest
                {
                    EntityLogicalName = entityLogicalName,
                    LogicalName = attributeLogicalName,
                    RetrieveAsIfPublished = true
                };

                var response = (RetrieveAttributeResponse)_service.Execute(request);
                var attributeMetadata = response?.AttributeMetadata;

                if (attributeMetadata != null)
                {
                    _attributeMetadataCache[cacheKey] = attributeMetadata;
                }

                return attributeMetadata;
            }
            catch (Exception ex)
            {
                _tracer.Warning($"Unable to retrieve attribute metadata for {entityLogicalName}.{attributeLogicalName}: {ex.Message}");
                return null;
            }
        }

        private string ConvertToTextValue(object rawValue, string formattedValue)
        {
            if (rawValue == null) return null;

            if (rawValue is EntityReference entityRef)
            {
                if (!string.IsNullOrWhiteSpace(entityRef.Name))
                {
                    return entityRef.Name;
                }

                return formattedValue ?? entityRef.Id.ToString();
            }

            if (rawValue is OptionSetValue optionSet)
            {
                return formattedValue ?? optionSet.Value.ToString();
            }

            return rawValue.ToString();
        }

        private string ApplyTruncationIfNeeded(string textValue, AttributeMetadata targetAttributeMetadata, string targetAttributeLogicalName)
        {
            if (string.IsNullOrEmpty(textValue) || targetAttributeMetadata == null)
            {
                return textValue;
            }

            int? maxLength = null;

            if (targetAttributeMetadata is StringAttributeMetadata stringMetadata)
            {
                maxLength = stringMetadata.MaxLength;
            }
            else if (targetAttributeMetadata is MemoAttributeMetadata memoMetadata)
            {
                maxLength = memoMetadata.MaxLength;
            }

            if (!maxLength.HasValue || maxLength.Value <= 0)
            {
                return textValue;
            }

            if (textValue.Length <= maxLength.Value)
            {
                return textValue;
            }

            var truncatedLength = Math.Max(0, maxLength.Value - 1);
            var truncatedText = (truncatedLength > 0 ? textValue.Substring(0, truncatedLength) : string.Empty) + "…";

            _tracer.Warning($"Value for {targetAttributeLogicalName} truncated to {maxLength.Value} characters with ellipsis.");
            return truncatedText;
        }

        private void CascadeToRelatedEntity(Guid parentId, Dictionary<string, object> values, 
            RelatedEntityConfig relatedConfig, ModelCascadeConfiguration config)
        {
            _tracer.StartOperation($"CascadeToRelatedEntity-{relatedConfig.EntityName}");

            try
            {
                // Retrieve related records
                var relatedRecords = RetrieveRelatedRecords(parentId, relatedConfig, config);

                _tracer.Info($"Found {relatedRecords.Count} related {relatedConfig.EntityName} records");

                if (relatedRecords.Count == 0)
                {
                    _tracer.Info("No related records to update");
                    _tracer.EndOperation($"CascadeToRelatedEntity-{relatedConfig.EntityName}");
                    return;
                }

                // Update each related record
                int successCount = 0;
                int errorCount = 0;

                foreach (var relatedRecord in relatedRecords)
                {
                    try
                    {
                        UpdateRelatedRecord(relatedRecord, values);
                        successCount++;
                    }
                    catch (Exception ex)
                    {
                        errorCount++;
                        _tracer.Error($"Failed to update record {relatedRecord.Id}", ex);
                        // Continue with other records
                    }
                }

                _tracer.Info($"Update complete: {successCount} successful, {errorCount} failed");
                _tracer.EndOperation($"CascadeToRelatedEntity-{relatedConfig.EntityName}");
            }
            catch (Exception ex)
            {
                _tracer.Error($"Error cascading to {relatedConfig.EntityName}", ex);
                throw;
            }
        }

        private List<Entity> RetrieveRelatedRecords(Guid parentId, RelatedEntityConfig relatedConfig, 
            ModelCascadeConfiguration config)
        {
            _tracer.StartOperation("RetrieveRelatedRecords");

            try
            {
                QueryExpression query;

                if (relatedConfig.UseRelationship)
                {
                    // Build query using relationship
                    query = BuildQueryWithRelationship(parentId, relatedConfig, config);
                }
                else
                {
                    // Build query using lookup field
                    query = BuildQueryWithLookupField(parentId, relatedConfig);
                }

                _tracer.Debug($"Executing query for {relatedConfig.EntityName}");
                var results = _service.RetrieveMultiple(query);

                _tracer.EndOperation("RetrieveRelatedRecords");
                return results.Entities.ToList();
            }
            catch (Exception ex)
            {
                _tracer.Error("Error retrieving related records", ex);
                throw;
            }
        }

        private QueryExpression BuildQueryWithRelationship(Guid parentId, RelatedEntityConfig relatedConfig, 
            ModelCascadeConfiguration config)
        {
            var query = new QueryExpression(relatedConfig.EntityName)
            {
                ColumnSet = new ColumnSet(true), // Retrieve all columns for update context
                NoLock = true
            };

            // Add relationship link
            var link = new LinkEntity
            {
                LinkFromEntityName = relatedConfig.EntityName,
                LinkToEntityName = config.ParentEntity,
                LinkFromAttributeName = relatedConfig.RelationshipName.ToLower() + "id", // Convention-based
                LinkToAttributeName = config.ParentEntity + "id",
                JoinOperator = JoinOperator.Inner
            };

            link.LinkCriteria.AddCondition(config.ParentEntity + "id", ConditionOperator.Equal, parentId);
            query.LinkEntities.Add(link);

            // Add filter criteria if specified
            if (!string.IsNullOrWhiteSpace(relatedConfig.FilterCriteria))
            {
                AddFilterCriteria(query, relatedConfig.FilterCriteria);
            }

            return query;
        }

        private QueryExpression BuildQueryWithLookupField(Guid parentId, RelatedEntityConfig relatedConfig)
        {
            var query = new QueryExpression(relatedConfig.EntityName)
            {
                ColumnSet = new ColumnSet(true),
                NoLock = true
            };

            // Add condition for lookup field
            query.Criteria.AddCondition(relatedConfig.LookupFieldName, ConditionOperator.Equal, parentId);

            // Add filter criteria if specified
            if (!string.IsNullOrWhiteSpace(relatedConfig.FilterCriteria))
            {
                AddFilterCriteria(query, relatedConfig.FilterCriteria);
            }

            return query;
        }

        private void AddFilterCriteria(QueryExpression query, string filterCriteria)
        {
            try
            {
                // Parse filter criteria (simple format: field|operator|value;field|operator|value)
                var filters = filterCriteria.Split(';');

                foreach (var filter in filters)
                {
                    if (string.IsNullOrWhiteSpace(filter)) continue;

                    var parts = filter.Split('|');
                    if (parts.Length != 3)
                    {
                        _tracer.Warning($"Invalid filter format: {filter}. Expected format: field|operator|value");
                        continue;
                    }

                    var field = parts[0].Trim();
                    var operatorStr = parts[1].Trim();
                    var value = parts[2].Trim();

                    // Parse operator
                    ConditionOperator conditionOperator = ParseOperator(operatorStr);

                    // Parse value based on type
                    object parsedValue = ParseValue(value);

                    query.Criteria.AddCondition(field, conditionOperator, parsedValue);
                    _tracer.Debug($"Added filter: {field} {operatorStr} {value}");
                }
            }
            catch (Exception ex)
            {
                _tracer.Error("Error parsing filter criteria", ex);
                throw new InvalidPluginExecutionException($"Invalid filter criteria format: {ex.Message}", ex);
            }
        }

        private ConditionOperator ParseOperator(string operatorStr)
        {
            switch (operatorStr.ToLower())
            {
                case "equal":
                case "eq":
                case "=":
                    return ConditionOperator.Equal;
                case "notequal":
                case "ne":
                case "!=":
                    return ConditionOperator.NotEqual;
                case "greaterthan":
                case "gt":
                case ">":
                    return ConditionOperator.GreaterThan;
                case "lessthan":
                case "lt":
                case "<":
                    return ConditionOperator.LessThan;
                case "in":
                    return ConditionOperator.In;
                case "notin":
                    return ConditionOperator.NotIn;
                case "null":
                    return ConditionOperator.Null;
                case "notnull":
                    return ConditionOperator.NotNull;
                case "like":
                    return ConditionOperator.Like;
                default:
                    throw new InvalidPluginExecutionException($"Unknown operator: {operatorStr}");
            }
        }

        private object ParseValue(string value)
        {
            if (value.Equals("null", StringComparison.OrdinalIgnoreCase))
                return null;

            if (value.Equals("true", StringComparison.OrdinalIgnoreCase))
                return true;

            if (value.Equals("false", StringComparison.OrdinalIgnoreCase))
                return false;

            if (int.TryParse(value, out int intValue))
                return intValue;

            if (Guid.TryParse(value, out Guid guidValue))
                return guidValue;

            return value; // Return as string
        }

        private void UpdateRelatedRecord(Entity relatedRecord, Dictionary<string, object> values)
        {
            var updateEntity = new Entity(relatedRecord.LogicalName, relatedRecord.Id);

            foreach (var kvp in values)
            {
                updateEntity[kvp.Key] = kvp.Value;
            }

            _tracer.Debug($"Updating {relatedRecord.LogicalName} record {relatedRecord.Id}");
            _service.Update(updateEntity);
        }

        private bool AreValuesEqual(object value1, object value2)
        {
            if (value1 == null && value2 == null) return true;
            if (value1 == null || value2 == null) return false;

            // Handle EntityReference comparison
            if (value1 is EntityReference ref1 && value2 is EntityReference ref2)
            {
                return ref1.Id == ref2.Id && ref1.LogicalName == ref2.LogicalName;
            }

            // Handle OptionSetValue comparison
            if (value1 is OptionSetValue opt1 && value2 is OptionSetValue opt2)
            {
                return opt1.Value == opt2.Value;
            }

            // Handle Money comparison
            if (value1 is Money money1 && value2 is Money money2)
            {
                return money1.Value == money2.Value;
            }

            return value1.Equals(value2);
        }
    }
}
