using CascadeFields.Plugin.Helpers;
using CascadeFields.Plugin.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CascadeFields.Plugin.Services
{
    /// <summary>
    /// Service for executing cascade field operations
    /// </summary>
    public class CascadeService
    {
        private readonly IOrganizationService _service;
        private readonly PluginTracer _tracer;

        public CascadeService(IOrganizationService service, PluginTracer tracer)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
        }

        /// <summary>
        /// Checks if any trigger fields have changed
        /// </summary>
        public bool HasTriggerFieldChanged(Entity target, Entity preImage, CascadeConfiguration config)
        {
            _tracer.StartOperation("HasTriggerFieldChanged");

            var triggerFields = config.FieldMappings
                .Where(m => m.IsTriggerField)
                .Select(m => m.SourceField)
                .ToList();

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
        public void CascadeFieldValues(Entity target, Entity preImage, CascadeConfiguration config)
        {
            _tracer.StartOperation("CascadeFieldValues");

            try
            {
                // Get values to cascade
                var valuesToCascade = GetValuesToCascade(target, preImage, config);

                if (valuesToCascade.Count == 0)
                {
                    _tracer.Info("No values to cascade");
                    _tracer.EndOperation("CascadeFieldValues");
                    return;
                }

                _tracer.Info($"Cascading {valuesToCascade.Count} field values");

                // Process each related entity configuration
                foreach (var relatedEntityConfig in config.RelatedEntities)
                {
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

        private Dictionary<string, object> GetValuesToCascade(Entity target, Entity preImage, CascadeConfiguration config)
        {
            var values = new Dictionary<string, object>();

            foreach (var mapping in config.FieldMappings)
            {
                // Prefer target over preImage
                if (target.Contains(mapping.SourceField))
                {
                    values[mapping.TargetField] = target[mapping.SourceField];
                    _tracer.Debug($"Mapping: {mapping.SourceField} -> {mapping.TargetField} = {target[mapping.SourceField]}");
                }
                else if (preImage != null && preImage.Contains(mapping.SourceField))
                {
                    values[mapping.TargetField] = preImage[mapping.SourceField];
                    _tracer.Debug($"Mapping (from preImage): {mapping.SourceField} -> {mapping.TargetField} = {preImage[mapping.SourceField]}");
                }
            }

            return values;
        }

        private void CascadeToRelatedEntity(Guid parentId, Dictionary<string, object> values, 
            RelatedEntityConfig relatedConfig, CascadeConfiguration config)
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
            CascadeConfiguration config)
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
            CascadeConfiguration config)
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
