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
        /// Applies mapped values from parent to the current child record when the child is created
        /// or when its parent lookup is (re)assigned. Uses the same mapping config; no duplicate child mappings required.
        /// </summary>
        /// <param name="context">Plugin execution context to determine stage/message.</param>
        /// <param name="target">The child entity from context.InputParameters["Target"]</param>
        /// <param name="preImage">Optional pre image for update comparisons</param>
        /// <param name="config">Cascade configuration</param>
        public void ApplyParentValuesToChildOnAttachOrCreate(IPluginExecutionContext context, Entity target, Entity preImage, ModelCascadeConfiguration config)
        {
            _tracer.StartOperation("ApplyParentValuesToChildOnAttachOrCreate");

            var relatedConfigs = config.RelatedEntities?
                .Where(r => r.EntityName.Equals(context.PrimaryEntityName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (relatedConfigs == null || relatedConfigs.Count == 0)
            {
                _tracer.Info($"No related entity configuration found for '{context.PrimaryEntityName}'. Skipping.");
                _tracer.EndOperation("ApplyParentValuesToChildOnAttachOrCreate");
                return;
            }

            var aggregatedValues = new Dictionary<string, object>();

            foreach (var relatedConfig in relatedConfigs)
            {
                // Determine lookup field on the child that points to the parent
                var lookupField = !string.IsNullOrWhiteSpace(relatedConfig.LookupFieldName)
                    ? relatedConfig.LookupFieldName
                    : DetermineLookupFieldFromRelationship(relatedConfig.RelationshipName, config.ParentEntity);

                if (string.IsNullOrWhiteSpace(lookupField))
                {
                    throw new InvalidPluginExecutionException(
                        $"Unable to determine lookup field for child entity '{relatedConfig.EntityName}'. " +
                        "Set 'lookupFieldName' in configuration.");
                }

                // Ensure there is a parent reference on create; for update, ensure it changed
                if (string.Equals(context.MessageName, "Create", StringComparison.OrdinalIgnoreCase))
                {
                    if (!target.Contains(lookupField))
                    {
                        _tracer.Info($"Create did not include '{lookupField}' on child; skipping this mapping set.");
                        continue;
                    }
                }
                else if (string.Equals(context.MessageName, "Update", StringComparison.OrdinalIgnoreCase))
                {
                    var changed = HasLookupChanged(target, preImage, lookupField);
                    if (!changed)
                    {
                        _tracer.Info($"Update did not change '{lookupField}'; skipping mapping set for this lookup.");
                        continue;
                    }
                }
                else
                {
                    _tracer.Info($"Unsupported message '{context.MessageName}' for child attach handling.");
                    _tracer.EndOperation("ApplyParentValuesToChildOnAttachOrCreate");
                    return;
                }

                var parentRef = target.GetAttributeValue<EntityReference>(lookupField);
                if (parentRef == null || parentRef.Id == Guid.Empty)
                {
                    _tracer.Info($"Lookup '{lookupField}' has no parent reference; skipping this mapping set.");
                    continue;
                }

                // Optional: ensure lookup points to the configured parent entity (handles polymorphic lookups)
                if (!string.IsNullOrWhiteSpace(config.ParentEntity) &&
                    !config.ParentEntity.Equals(parentRef.LogicalName, StringComparison.OrdinalIgnoreCase))
                {
                    _tracer.Info($"Lookup '{lookupField}' points to '{parentRef.LogicalName}', not configured parent '{config.ParentEntity}'. Skipping this mapping set.");
                    continue;
                }

                // Respect filterCriteria by verifying the single child matches (if provided)
                if (!string.IsNullOrWhiteSpace(relatedConfig.FilterCriteria) && target.Id != Guid.Empty)
                {
                    if (!ChildMatchesFilter(target.LogicalName, target.Id, relatedConfig))
                    {
                        _tracer.Info("Child does not meet filter criteria; skipping this mapping set.");
                        continue;
                    }
                }

                // Retrieve parent with needed source fields
                var sourceFields = relatedConfig.FieldMappings?.Select(m => m.SourceField).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? Array.Empty<string>();
                if (sourceFields.Length == 0)
                {
                    _tracer.Info("No field mappings defined for child; skipping this mapping set.");
                    continue;
                }

                var parent = _service.Retrieve(config.ParentEntity, parentRef.Id, new ColumnSet(sourceFields));

                // Build values to apply to the child using mapping logic (string conversion/truncation aware)
                var values = new Dictionary<string, object>();
                foreach (var mapping in relatedConfig.FieldMappings)
                {
                    if (!parent.Contains(mapping.SourceField))
                    {
                        continue;
                    }
                    var mapped = GetMappedValueForTarget(parent, mapping, relatedConfig.EntityName);
                    values[mapping.TargetField] = mapped;
                }

                if (values.Count == 0)
                {
                    _tracer.Info("No values resolved from parent to apply to child for this mapping set.");
                    continue;
                }

                foreach (var kvp in values)
                {
                    aggregatedValues[kvp.Key] = kvp.Value;
                }
            }

            if (aggregatedValues.Count == 0)
            {
                _tracer.Info("No values resolved from any related entity configuration; nothing to apply to child.");
                _tracer.EndOperation("ApplyParentValuesToChildOnAttachOrCreate");
                return;
            }

            // PreOperation: assign to target so the platform writes them as part of the same transaction
            if (context.Stage == 20 /* PreOperation */)
            {
                foreach (var kvp in aggregatedValues)
                {
                    target[kvp.Key] = kvp.Value;
                }
                _tracer.Info($"Applied {aggregatedValues.Count} mapped values to child in PreOperation.");
            }
            else
            {
                // Fallback: update after operation if not running PreOperation
                if (target.Id == Guid.Empty)
                {
                    _tracer.Warning("Cannot post-update child without an ID; ensure PreOperation stage for Create.");
                }
                else
                {
                    var update = new Entity(target.LogicalName, target.Id);
                    foreach (var kvp in aggregatedValues)
                    {
                        update[kvp.Key] = kvp.Value;
                    }
                    _service.Update(update);
                    _tracer.Info($"Updated child with {aggregatedValues.Count} mapped values post-operation.");
                }
            }

            _tracer.EndOperation("ApplyParentValuesToChildOnAttachOrCreate");
        }

        /// <summary>
        /// Determines whether the configured lookup on the child changed between pre-image and target.
        /// </summary>
        private bool HasLookupChanged(Entity target, Entity preImage, string lookupField)
        {
            if (!target.Contains(lookupField))
            {
                return false;
            }

            var newRef = target.GetAttributeValue<EntityReference>(lookupField);
            var oldRef = preImage?.GetAttributeValue<EntityReference>(lookupField);
            return !AreValuesEqual(newRef, oldRef);
        }

        /// <summary>
        /// Verifies a single child row matches the optional filter criteria before applying mappings.
        /// </summary>
        private bool ChildMatchesFilter(string childEntityName, Guid childId, RelatedEntityConfig relatedConfig)
        {
            try
            {
                var query = new QueryExpression(childEntityName)
                {
                    ColumnSet = new ColumnSet(false),
                    NoLock = true,
                    TopCount = 1
                };

                // Primary key is consistently <logicalname>id
                query.Criteria.AddCondition(childEntityName + "id", ConditionOperator.Equal, childId);

                if (!string.IsNullOrWhiteSpace(relatedConfig.FilterCriteria))
                {
                    AddFilterCriteria(query, relatedConfig.FilterCriteria);
                }

                var result = _service.RetrieveMultiple(query);
                return result.Entities.Count > 0;
            }
            catch (Exception ex)
            {
                _tracer.Warning($"Error evaluating child filter criteria: {ex.Message}. Proceeding without filter check.");
                return true; // be permissive if parsing fails to avoid blocking create/update
            }
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

        /// <summary>
        /// Builds a dictionary of target-field values for a specific related entity based on the mapping set.
        /// </summary>
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

        /// <summary>
        /// Resolves the mapped value for a target field, performing type-aware conversions when needed.
        /// </summary>
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

        /// <summary>
        /// Retrieves and caches attribute metadata so type and length can guide conversions and truncation.
        /// </summary>
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

        /// <summary>
        /// Converts a lookup/optionset/primitive value into a user-friendly string using formatted values when available.
        /// </summary>
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

        /// <summary>
        /// Applies safe truncation with ellipsis when the target attribute is length-constrained.
        /// </summary>
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

        /// <summary>
        /// Executes the cascade for a single related-entity configuration, retrieving children and applying updates.
        /// </summary>
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

                // Update related records using batched ExecuteMultiple for performance
                UpdateRelatedRecordsBatch(relatedRecords, values);
                _tracer.EndOperation($"CascadeToRelatedEntity-{relatedConfig.EntityName}");
            }
            catch (Exception ex)
            {
                _tracer.Error($"Error cascading to {relatedConfig.EntityName}", ex);
                throw;
            }
        }

        /// <summary>
        /// Retrieves related child records using either a relationship or explicit lookup field path.
        /// </summary>
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

        /// <summary>
        /// Builds a query that uses the configured relationship name to locate child rows.
        /// </summary>
        private QueryExpression BuildQueryWithRelationship(Guid parentId, RelatedEntityConfig relatedConfig, 
            ModelCascadeConfiguration config)
        {
            var query = new QueryExpression(relatedConfig.EntityName)
            {
                ColumnSet = new ColumnSet(false), // Only retrieve ID - we don't need other columns for updates
                NoLock = true,
                TopCount = 5000 // Safety limit to prevent unbounded queries
            };

            // IMPORTANT: UseRelationship mode is deprecated and may not work correctly.
            // It's strongly recommended to use UseRelationship=false and specify LookupFieldName instead.
            // This implementation falls back to direct lookup field query if relationship-based query fails.
            _tracer.Warning($"Using relationship-based query for '{relatedConfig.RelationshipName}'. Consider using UseRelationship=false with explicit LookupFieldName for better reliability.");
            
            // Attempt to derive lookup field from relationship name
            // This is a best-effort approach and may not work for all relationships
            var lookupField = DetermineLookupFieldFromRelationship(relatedConfig.RelationshipName, config.ParentEntity);
            
            if (string.IsNullOrWhiteSpace(lookupField))
            {
                throw new InvalidPluginExecutionException(
                    $"Unable to determine lookup field for relationship '{relatedConfig.RelationshipName}'. " +
                    "Please use UseRelationship=false and specify LookupFieldName explicitly.");
            }
            
            // Use direct condition instead of link entity for better performance
            query.Criteria.AddCondition(lookupField, ConditionOperator.Equal, parentId);

            // Add filter criteria if specified
            if (!string.IsNullOrWhiteSpace(relatedConfig.FilterCriteria))
            {
                AddFilterCriteria(query, relatedConfig.FilterCriteria);
            }

            return query;
        }

        /// <summary>
        /// Builds a query that filters child rows by an explicit lookup field value.
        /// </summary>
        private QueryExpression BuildQueryWithLookupField(Guid parentId, RelatedEntityConfig relatedConfig)
        {
            var query = new QueryExpression(relatedConfig.EntityName)
            {
                ColumnSet = new ColumnSet(false), // Only retrieve ID - we don't need other columns for updates
                NoLock = true,
                TopCount = 5000 // Safety limit to prevent unbounded queries
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

        /// <summary>
        /// Parses simple pipe-delimited filter criteria into QueryExpression conditions with basic validation.
        /// </summary>
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

                    // Validate field name to prevent injection attacks
                    ValidateFilterFieldName(field, query.EntityName);

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

        /// <summary>
        /// Converts a user-provided operator string into a ConditionOperator enum value.
        /// </summary>
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

        /// <summary>
        /// Parses string literal values into typed objects used by query conditions (supports null/bool/int/guid/string).
        /// </summary>
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

        /// <summary>
        /// Updates related records in batches using ExecuteMultipleRequest for better performance
        /// </summary>
        private void UpdateRelatedRecordsBatch(List<Entity> relatedRecords, Dictionary<string, object> values)
        {
            const int batchSize = 50; // Optimal batch size for ExecuteMultiple
            var requests = new OrganizationRequestCollection();
            int successCount = 0;
            int errorCount = 0;
            int processedCount = 0;

            _tracer.Info($"Updating {relatedRecords.Count} records in batches of {batchSize}");

            foreach (var relatedRecord in relatedRecords)
            {
                var updateEntity = new Entity(relatedRecord.LogicalName, relatedRecord.Id);
                foreach (var kvp in values)
                {
                    updateEntity[kvp.Key] = kvp.Value;
                }

                requests.Add(new UpdateRequest { Target = updateEntity });

                // Execute batch when we reach batch size or this is the last record
                if (requests.Count >= batchSize || processedCount + requests.Count >= relatedRecords.Count)
                {
                    var batchResults = ExecuteBatch(requests);
                    successCount += batchResults.SuccessCount;
                    errorCount += batchResults.ErrorCount;
                    processedCount += requests.Count;
                    
                    _tracer.Debug($"Batch progress: {processedCount}/{relatedRecords.Count} records processed");
                    requests.Clear();
                }
            }

            _tracer.Info($"Update complete: {successCount} successful, {errorCount} failed");
        }

        /// <summary>
        /// Executes a batch of update requests using ExecuteMultipleRequest
        /// </summary>
        private (int SuccessCount, int ErrorCount) ExecuteBatch(OrganizationRequestCollection requests)
        {
            if (requests.Count == 0)
                return (0, 0);

            try
            {
                var multipleRequest = new ExecuteMultipleRequest
                {
                    Settings = new ExecuteMultipleSettings
                    {
                        ContinueOnError = true,
                        ReturnResponses = true
                    },
                    Requests = requests
                };

                _tracer.Debug($"Executing batch of {requests.Count} update requests");
                var response = (ExecuteMultipleResponse)_service.Execute(multipleRequest);

                int successCount = 0;
                int errorCount = 0;

                for (int i = 0; i < response.Responses.Count; i++)
                {
                    var responseItem = response.Responses[i];
                    if (responseItem.Fault != null)
                    {
                        errorCount++;
                        var updateRequest = requests[responseItem.RequestIndex] as UpdateRequest;
                        var recordId = updateRequest?.Target?.Id ?? Guid.Empty;
                        _tracer.Error($"Batch update failed for record {recordId} at index {responseItem.RequestIndex}: {responseItem.Fault.Message}");
                    }
                    else
                    {
                        successCount++;
                    }
                }

                return (successCount, errorCount);
            }
            catch (Exception ex)
            {
                _tracer.Error($"ExecuteMultiple batch failed completely", ex);
                return (0, requests.Count);
            }
        }

        /// <summary>
        /// Determines the lookup field name from a relationship name
        /// </summary>
        private string DetermineLookupFieldFromRelationship(string relationshipName, string parentEntity)
        {
            // Common patterns for lookup field names:
            // 1. Direct: "parentaccountid" for account
            // 2. Named: "accountid" for account relationship
            // 3. Custom: various patterns
            
            // Try common patterns
            var candidates = new List<string>
            {
                $"parent{parentEntity}id",
                $"{parentEntity}id",
                relationshipName.ToLower()
            };

            // Return first candidate (most common pattern)
            // Note: This is a best-effort heuristic. Using explicit LookupFieldName is always preferred.
            return candidates[0];
        }

        /// <summary>
        /// Validates that the filter field name is safe and exists in the entity metadata
        /// </summary>
        private void ValidateFilterFieldName(string fieldName, string entityName)
        {
            // Basic validation: field name should only contain alphanumeric characters and underscores
            if (string.IsNullOrWhiteSpace(fieldName))
            {
                throw new InvalidPluginExecutionException("Filter field name cannot be empty.");
            }

            // Check for basic injection patterns
            if (fieldName.Contains(";") || fieldName.Contains("--") || fieldName.Contains("/*") || 
                fieldName.Contains("*/") || fieldName.Contains("'") || fieldName.Contains("\""))
            {
                throw new InvalidPluginExecutionException($"Filter field name '{fieldName}' contains invalid characters.");
            }

            // Validate field name format (alphanumeric + underscore only)
            if (!System.Text.RegularExpressions.Regex.IsMatch(fieldName, @"^[a-zA-Z0-9_]+$"))
            {
                throw new InvalidPluginExecutionException($"Filter field name '{fieldName}' contains invalid characters. Only alphanumeric characters and underscores are allowed.");
            }

            // Optional: Verify field exists in entity metadata (adds overhead but increases security)
            // This is commented out by default for performance, but can be enabled for strict validation
            // var fieldMetadata = GetTargetAttributeMetadata(entityName, fieldName);
            // if (fieldMetadata == null)
            // {
            //     throw new InvalidPluginExecutionException($"Filter field '{fieldName}' does not exist on entity '{entityName}'.");
            // }
        }

        /// <summary>
        /// Compares CRM types and primitives for equality, handling common SDK wrapper types safely.
        /// </summary>
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
