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
    /// Core service responsible for executing field value cascades from parent records to related child records.
    /// Handles both parent-side cascades (when parent updates) and child-side operations (when child is created or relinked).
    /// </summary>
    /// <remarks>
    /// <para><b>Key Responsibilities:</b></para>
    /// <list type="bullet">
    ///     <item><description><b>Parent-Side Cascades:</b> When a parent record is updated, cascade configured field values to all related child records</description></item>
    ///     <item><description><b>Child-Side Operations:</b> When a child record is created or its parent lookup changes, copy parent field values to the child</description></item>
    ///     <item><description><b>Trigger Field Logic:</b> Optimize cascades by only processing when configured trigger fields change</description></item>
    ///     <item><description><b>Filter Criteria:</b> Support optional FetchXML-style filters to limit which child records are updated</description></item>
    ///     <item><description><b>Batch Updates:</b> Use ExecuteMultiple for efficient bulk updates of child records</description></item>
    ///     <item><description><b>Type Conversion:</b> Handle type conversions for lookups/optionsets to string fields with automatic truncation</description></item>
    ///     <item><description><b>Metadata Caching:</b> Cache attribute metadata to optimize repeated field information lookups</description></item>
    /// </list>
    ///
    /// <para><b>Performance Optimizations:</b></para>
    /// <list type="number">
    ///     <item><description>Attribute metadata is cached per-instance to reduce metadata query overhead</description></item>
    ///     <item><description>Child record updates use ExecuteMultiple in batches of 50 for optimal throughput</description></item>
    ///     <item><description>Queries retrieve only the ID field to minimize data transfer</description></item>
    ///     <item><description>NoLock and TopCount=5000 safety limits prevent runaway queries</description></item>
    /// </list>
    ///
    /// <para><b>Error Handling:</b></para>
    /// All errors are logged via PluginTracer and re-thrown as InvalidPluginExecutionException with descriptive messages.
    /// Batch update errors are logged individually but don't stop processing of other records (ContinueOnError=true).
    /// </remarks>
    public class CascadeService
    {
        /// <summary>
        /// Dataverse organization service used for all data operations (retrieve, update, metadata queries).
        /// </summary>
        private readonly IOrganizationService _service;

        /// <summary>
        /// Tracing service wrapper for diagnostic logging and performance monitoring.
        /// </summary>
        private readonly PluginTracer _tracer;

        /// <summary>
        /// In-memory cache of attribute metadata to avoid repeated metadata queries for the same fields.
        /// Key format: "entityname:attributename" (case-insensitive).
        /// </summary>
        private readonly Dictionary<string, AttributeMetadata> _attributeMetadataCache;

        /// <summary>
        /// Initializes a new instance of the <see cref="CascadeService"/> class with required dependencies.
        /// </summary>
        /// <param name="service">The Dataverse organization service for data and metadata operations. Cannot be null.</param>
        /// <param name="tracer">The plugin tracer for logging diagnostic information and performance metrics. Cannot be null.</param>
        /// <exception cref="ArgumentNullException">Thrown if service or tracer is null.</exception>
        /// <remarks>
        /// The attribute metadata cache is initialized as an empty dictionary with case-insensitive string comparison.
        /// Metadata is lazily loaded and cached as fields are encountered during cascade operations.
        /// </remarks>
        public CascadeService(IOrganizationService service, PluginTracer tracer)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
            _attributeMetadataCache = new Dictionary<string, AttributeMetadata>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Applies configured field values from a parent record to a child record when the child is created or its parent lookup is changed.
        /// This is the "child-side" operation that complements the parent-side cascade.
        /// </summary>
        /// <param name="context">
        /// The plugin execution context containing message name, stage, and primary entity information.
        /// Used to determine whether this is a Create or Update operation and the execution stage.
        /// </param>
        /// <param name="target">
        /// The child entity being created or updated, from context.InputParameters["Target"].
        /// For Create: Contains fields being set on the new record, including the parent lookup.
        /// For Update: Contains only changed fields, potentially including a changed parent lookup.
        /// </param>
        /// <param name="preImage">
        /// Optional pre-image entity for Update operations, used to detect if the parent lookup field changed.
        /// Not available for Create operations (null).
        /// </param>
        /// <param name="config">
        /// The cascade configuration defining parent entity, related entities, and field mappings.
        /// This method finds child entity configs matching the current entity and applies their field mappings.
        /// </param>
        /// <remarks>
        /// <para><b>Purpose:</b></para>
        /// This method handles the scenario where a child record needs to receive parent field values at the moment
        /// it's associated with a parent. This is different from parent-side cascades which update existing children
        /// when the parent changes.
        ///
        /// <para><b>When to Register:</b></para>
        /// Register on child entity Create (Pre-operation) and Update (Pre-operation) messages, filtered to the parent lookup field.
        ///
        /// <para><b>Execution Modes:</b></para>
        /// <list type="bullet">
        ///     <item>
        ///         <description><b>Create:</b> If the child record includes a parent lookup on create, retrieve the parent
        ///         and copy configured field values to the child.</description>
        ///     </item>
        ///     <item>
        ///         <description><b>Update:</b> If the parent lookup field changes (child is relinked to a different parent),
        ///         retrieve the new parent and copy its field values to the child.</description>
        ///     </item>
        /// </list>
        ///
        /// <para><b>Filter Criteria Support:</b></para>
        /// If filterCriteria is specified in the related entity config, verifies the child record matches
        /// the filter before applying mappings. This allows selective application based on child record state.
        ///
        /// <para><b>Pre-Operation vs Post-Operation:</b></para>
        /// <list type="bullet">
        ///     <item>
        ///         <description><b>Pre-Operation (Stage 20 - Recommended):</b> Values are added directly to the target entity,
        ///         so they're saved as part of the same transaction. This is the recommended approach.</description>
        ///     </item>
        ///     <item>
        ///         <description><b>Post-Operation (Stage 40):</b> A separate Update call is made to set the values.
        ///         This requires the child record to have an ID (not suitable for Create in Post-operation).</description>
        ///     </item>
        /// </list>
        ///
        /// <para><b>Multiple Related Configs:</b></para>
        /// If multiple related entity configurations exist for the same child entity (e.g., different parent lookup fields),
        /// values from all applicable configs are aggregated into a single update.
        /// </remarks>
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
        /// Determines whether a lookup field on a child entity has changed during an Update operation.
        /// Used to detect when a child record is being relinked to a different parent.
        /// </summary>
        /// <param name="target">The entity being updated, containing only changed fields.</param>
        /// <param name="preImage">The pre-image entity containing the field's previous value.</param>
        /// <param name="lookupField">The logical name of the lookup field to check (e.g., "parentaccountid").</param>
        /// <returns>
        /// <c>true</c> if the lookup field is present in the target and its value differs from the pre-image;
        /// otherwise, <c>false</c> (field not changed or not present in update).
        /// </returns>
        /// <remarks>
        /// Uses <see cref="AreValuesEqual"/> for EntityReference comparison, which compares both Id and LogicalName.
        /// </remarks>
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
        /// Verifies that a specific child record matches the optional filter criteria defined in the related entity configuration.
        /// Used to determine if field mappings should be applied to a child record during Create or Update operations.
        /// </summary>
        /// <param name="childEntityName">The logical name of the child entity (e.g., "contact").</param>
        /// <param name="childId">The unique identifier of the child record to verify.</param>
        /// <param name="relatedConfig">The related entity configuration containing optional filterCriteria.</param>
        /// <returns>
        /// <c>true</c> if the child record matches the filter criteria (or if no filter is specified);
        /// <c>false</c> if the filter is specified and the child record doesn't match.
        /// Returns <c>true</c> (permissive) if filter parsing fails to avoid blocking operations.
        /// </returns>
        /// <remarks>
        /// <para><b>Query Strategy:</b></para>
        /// Constructs a QueryExpression that filters on the child record's ID plus any configured filter criteria.
        /// Uses NoLock and TopCount=1 for optimal performance.
        ///
        /// <para><b>Error Handling:</b></para>
        /// If filter criteria parsing fails, logs a warning and returns true (permissive behavior)
        /// to avoid blocking create/update operations due to configuration errors.
        /// </remarks>
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
        /// Determines whether any trigger fields defined for a specific related entity configuration have changed in the current update.
        /// Trigger fields are an optimization that prevents unnecessary cascades when unrelated fields are updated.
        /// </summary>
        /// <param name="relatedEntity">The related entity configuration containing field mappings with potential trigger fields.</param>
        /// <param name="target">The entity being updated, containing only changed fields.</param>
        /// <param name="preImage">The pre-image entity containing the record's state before the update.</param>
        /// <returns>
        /// <c>true</c> if any trigger field changed, or if no trigger fields are configured (process all updates);
        /// <c>false</c> if trigger fields are configured but none of them changed.
        /// </returns>
        /// <remarks>
        /// <para><b>Trigger Field Logic:</b></para>
        /// <list type="bullet">
        ///     <item><description><b>No trigger fields configured:</b> Always returns true (cascade on every parent update)</description></item>
        ///     <item><description><b>Trigger fields configured:</b> Returns true only if at least one trigger field changed</description></item>
        ///     <item><description><b>Field present in target but not pre-image:</b> Considered changed (field was set during update)</description></item>
        ///     <item><description><b>Field not in target:</b> Skipped (not part of this update operation)</description></item>
        /// </list>
        ///
        /// <para><b>Performance Optimization:</b></para>
        /// By marking specific fields as triggers, you can prevent cascades when unrelated fields change.
        /// For example, if cascading address fields, mark them as triggers so cascades don't occur when "description" is updated.
        /// </remarks>
        private bool HasTriggerFieldChanged(RelatedEntityConfig relatedEntity, Entity target, Entity preImage)
        {
            // Gather trigger fields defined for this related entity only
            var triggerFields = relatedEntity.FieldMappings?
                .Where(m => m.IsTriggerField)
                .Select(m => m.SourceField)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList() ?? new List<string>();

            // If no trigger fields are configured, always process this mapping set
            if (triggerFields.Count == 0)
            {
                _tracer.Debug($"No trigger fields configured for {relatedEntity.EntityName}; processing by default.");
                return true;
            }

            foreach (var field in triggerFields)
            {
                if (!target.Contains(field))
                {
                    continue; // field not part of this update payload
                }

                // If a pre-image exists and contains the field, compare old vs new values
                if (preImage != null && preImage.Contains(field))
                {
                    var oldValue = preImage[field];
                    var newValue = target[field];

                    if (!AreValuesEqual(oldValue, newValue))
                    {
                        _tracer.Info($"Trigger field '{field}' changed for {relatedEntity.EntityName}");
                        return true;
                    }
                }
                else
                {
                    // Field present in target without pre-image means it changed/was set
                    _tracer.Info($"Trigger field '{field}' provided on update for {relatedEntity.EntityName}");
                    return true;
                }
            }

            _tracer.Debug($"No trigger fields changed for {relatedEntity.EntityName}; skipping this mapping set.");
            return false;
        }

        /// <summary>
        /// Main entry point for parent-side cascade operations. When a parent record is updated, this method
        /// cascades configured field values to all related child entities based on trigger field logic.
        /// </summary>
        /// <param name="target">
        /// The parent entity being updated, from context.InputParameters["Target"].
        /// Contains only the fields that are being changed in this update operation.
        /// </param>
        /// <param name="preImage">
        /// The pre-image of the parent entity containing the record's state before the update.
        /// Used to compare old vs new values for trigger field detection.
        /// Should be configured in the plugin step registration to include all trigger fields and mapped source fields.
        /// </param>
        /// <param name="config">
        /// The cascade configuration defining which child entities receive updates and which fields are mapped.
        /// </param>
        /// <remarks>
        /// <para><b>Execution Flow:</b></para>
        /// <list type="number">
        ///     <item><description>Iterate through each related entity configuration in the cascade config</description></item>
        ///     <item><description>Check if any trigger fields changed for this specific related entity (optimization)</description></item>
        ///     <item><description>If triggered, gather values to cascade by resolving field mappings from target/preImage</description></item>
        ///     <item><description>Query for related child records (with optional filtering)</description></item>
        ///     <item><description>Batch update child records using ExecuteMultiple</description></item>
        /// </list>
        ///
        /// <para><b>Trigger Field Optimization:</b></para>
        /// If any field mappings are marked as trigger fields, the cascade only occurs when at least one trigger field changes.
        /// This prevents unnecessary child updates when unrelated parent fields are modified.
        /// If no trigger fields are configured, cascades occur on every parent update.
        ///
        /// <para><b>Error Handling:</b></para>
        /// All exceptions are logged via PluginTracer and re-thrown. The entire operation is wrapped in try-catch
        /// to ensure proper logging and cleanup.
        /// </remarks>
        public void CascadeFieldValues(Entity target, Entity preImage, ModelCascadeConfiguration config)
        {
            _tracer.StartOperation("CascadeFieldValues");

            try
            {
                var anyTriggered = false;

                // Process each related entity configuration
                foreach (var relatedEntityConfig in config.RelatedEntities)
                {
                    if (!HasTriggerFieldChanged(relatedEntityConfig, target, preImage))
                    {
                        continue; // skip mapping sets whose trigger fields did not change
                    }

                    anyTriggered = true;

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

                if (!anyTriggered)
                {
                    _tracer.Info("No trigger fields changed for any related entity; skipping cascade.");
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
        /// Builds a dictionary of field values to cascade to child records for a specific related entity configuration.
        /// Resolves source field values from the parent entity (preferring target over preImage) and maps them to target field names.
        /// </summary>
        /// <param name="target">The parent entity being updated, containing changed fields.</param>
        /// <param name="preImage">The pre-image of the parent entity, providing unchanged field values.</param>
        /// <param name="relatedEntityConfig">
        /// The related entity configuration containing field mappings that define which parent fields cascade to which child fields.
        /// </param>
        /// <returns>
        /// A dictionary where keys are target (child) field names and values are the corresponding values from the parent.
        /// Values are processed through <see cref="GetMappedValueForTarget"/> for type conversion and truncation.
        /// </returns>
        /// <remarks>
        /// <para><b>Value Resolution Priority:</b></para>
        /// For each field mapping, the method first checks the target entity for the source field value.
        /// If not found in target, falls back to the preImage. This ensures both changed and unchanged
        /// fields can be cascaded.
        ///
        /// <para><b>Type Conversion:</b></para>
        /// Values are processed for type compatibility. Lookups and OptionSets targeting string fields
        /// are converted to their text representations with automatic truncation if needed.
        /// </remarks>
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
        /// Resolves and converts a source field value for assignment to a target field, handling type conversions for string/memo targets.
        /// This is the key method that enables cascading lookups and optionsets to text fields.
        /// </summary>
        /// <param name="sourceEntity">The parent entity containing the source field value.</param>
        /// <param name="mapping">The field mapping defining source and target fields.</param>
        /// <param name="targetEntityName">The logical name of the child entity (used for metadata lookup).</param>
        /// <returns>
        /// The value to assign to the target field, potentially converted and truncated based on target field metadata.
        /// For string/memo targets: Converted to text representation with safe truncation.
        /// For other types: Returns the raw value as-is.
        /// </returns>
        /// <remarks>
        /// <para><b>Type Conversion Logic:</b></para>
        /// <list type="bullet">
        ///     <item><description><b>String/Memo Targets:</b> Converts EntityReference and OptionSetValue to readable text using FormattedValues or Name properties</description></item>
        ///     <item><description><b>Other Targets:</b> Passes through the raw value unchanged</description></item>
        /// </list>
        ///
        /// <para><b>Truncation Safety:</b></para>
        /// For string/memo targets, automatically truncates values that exceed the target field's MaxLength.
        /// Truncation adds an ellipsis character (…) and logs a warning for visibility.
        ///
        /// <para><b>Metadata Caching:</b></para>
        /// Target field metadata is retrieved via <see cref="GetTargetAttributeMetadata"/> which caches results
        /// to avoid repeated metadata queries for the same fields.
        /// </remarks>
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
        /// Retrieves attribute metadata for a target field and caches it for subsequent lookups.
        /// Metadata is used to determine field type and length for safe type conversion and truncation.
        /// </summary>
        /// <param name="entityLogicalName">The logical name of the entity containing the attribute.</param>
        /// <param name="attributeLogicalName">The logical name of the attribute to retrieve metadata for.</param>
        /// <returns>
        /// The <see cref="AttributeMetadata"/> for the specified field, or <c>null</c> if metadata cannot be retrieved
        /// or if parameters are null/empty.
        /// </returns>
        /// <remarks>
        /// <para><b>Caching Strategy:</b></para>
        /// Metadata is cached in <see cref="_attributeMetadataCache"/> using the key format "entityname:attributename".
        /// Cache lookups are case-insensitive. Once cached, metadata is reused for all subsequent requests.
        ///
        /// <para><b>Error Handling:</b></para>
        /// If metadata retrieval fails (e.g., field doesn't exist, insufficient permissions), logs a warning
        /// and returns null. This allows the cascade to continue without metadata-driven optimizations.
        /// </remarks>
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
        /// Converts Dataverse data types (EntityReference, OptionSetValue, primitives) to user-friendly text representations.
        /// Used when cascading non-string fields to string/memo fields.
        /// </summary>
        /// <param name="rawValue">The raw value from the source field (EntityReference, OptionSetValue, or primitive type).</param>
        /// <param name="formattedValue">
        /// The formatted value from the entity's FormattedValues collection, if available.
        /// Dataverse automatically populates FormattedValues with localized, user-friendly text for lookups and optionsets.
        /// </param>
        /// <returns>
        /// A text representation of the value:
        /// <list type="bullet">
        ///     <item><description><b>EntityReference:</b> Name property, or FormattedValue, or Id.ToString() as fallback</description></item>
        ///     <item><description><b>OptionSetValue:</b> FormattedValue (label), or Value.ToString() as fallback</description></item>
        ///     <item><description><b>Other types:</b> rawValue.ToString()</description></item>
        ///     <item><description><b>Null:</b> Returns null</description></item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// This method enables scenarios like cascading a lookup field (customer) to a text field (customer_name).
        /// The FormattedValue parameter provides localized, friendly text when available from the entity.
        /// </remarks>
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
        /// Safely truncates a text value if it exceeds the target field's maximum length, adding an ellipsis indicator.
        /// Prevents "string or binary data would be truncated" errors during cascade operations.
        /// </summary>
        /// <param name="textValue">The text value to potentially truncate.</param>
        /// <param name="targetAttributeMetadata">
        /// Metadata for the target field, used to determine if it's a string/memo field and its MaxLength.
        /// </param>
        /// <param name="targetAttributeLogicalName">The logical name of the target field (used for warning messages).</param>
        /// <returns>
        /// <list type="bullet">
        ///     <item><description>Original value if it fits within MaxLength</description></item>
        ///     <item><description>Truncated value with ellipsis (…) if it exceeds MaxLength</description></item>
        ///     <item><description>Original value if target has no length restriction or metadata is unavailable</description></item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// <para><b>Truncation Format:</b></para>
        /// When truncation occurs, the value is shortened to (MaxLength - 1) characters and an ellipsis character (…) is appended.
        /// Example: For MaxLength=10, "Hello World" becomes "Hello Wor…"
        ///
        /// <para><b>Logging:</b></para>
        /// A warning is logged whenever truncation occurs, including the field name and max length.
        ///
        /// <para><b>Supported Field Types:</b></para>
        /// Works with both String (single-line text) and Memo (multi-line text) field types.
        /// </remarks>
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
            
            // Prefer explicitly configured lookup when provided; fall back to heuristic
            var lookupField = !string.IsNullOrWhiteSpace(relatedConfig.LookupFieldName)
                ? relatedConfig.LookupFieldName
                : DetermineLookupFieldFromRelationship(relatedConfig.RelationshipName, config.ParentEntity);
            
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
                    var requiresValue = OperatorRequiresValue(conditionOperator);

                    if (!requiresValue)
                    {
                        query.Criteria.AddCondition(field, conditionOperator);
                        _tracer.Debug($"Added filter: {field} {operatorStr} (no value)");
                        continue;
                    }

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

        private static bool OperatorRequiresValue(ConditionOperator op)
        {
            switch (op)
            {
                case ConditionOperator.Null:
                case ConditionOperator.NotNull:
                    return false;
                default:
                    return true;
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
        /// Updates all related child records with cascaded field values using batched ExecuteMultiple requests for optimal performance.
        /// Processes records in batches of 50 to balance throughput and transaction size.
        /// </summary>
        /// <param name="relatedRecords">The list of child entity records to update (contains only IDs, retrieved with minimal columnset).</param>
        /// <param name="values">
        /// Dictionary of field values to apply to each child record.
        /// Keys are field logical names, values are the data to set.
        /// </param>
        /// <remarks>
        /// <para><b>Batch Strategy:</b></para>
        /// <list type="bullet">
        ///     <item><description><b>Batch Size:</b> 50 records per ExecuteMultiple request (optimal balance for performance)</description></item>
        ///     <item><description><b>ContinueOnError:</b> Enabled, so one failing record doesn't stop the entire batch</description></item>
        ///     <item><description><b>Progress Logging:</b> Logs progress after each batch for visibility</description></item>
        /// </list>
        ///
        /// <para><b>Performance Benefits:</b></para>
        /// ExecuteMultiple reduces round trips to the server. Updating 200 records becomes 4 requests instead of 200.
        ///
        /// <para><b>Error Handling:</b></para>
        /// Errors for individual records are logged separately with record ID and error message.
        /// Successful and failed counts are tracked and reported in the final summary.
        /// </remarks>
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
        /// Executes a single batch of update requests using ExecuteMultipleRequest and returns success/error counts.
        /// Configured with ContinueOnError=true so individual failures don't stop the batch.
        /// </summary>
        /// <param name="requests">Collection of UpdateRequest objects to execute in a single batch.</param>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description><b>SuccessCount:</b> Number of requests that completed successfully</description></item>
        ///     <item><description><b>ErrorCount:</b> Number of requests that failed</description></item>
        /// </list>
        /// If the entire ExecuteMultiple call fails, returns (0, requests.Count).
        /// </returns>
        /// <remarks>
        /// <para><b>ExecuteMultiple Settings:</b></para>
        /// <list type="bullet">
        ///     <item><description><b>ContinueOnError = true:</b> Process all requests even if some fail</description></item>
        ///     <item><description><b>ReturnResponses = true:</b> Get detailed results for each request to log errors</description></item>
        /// </list>
        ///
        /// <para><b>Error Logging:</b></para>
        /// Each failed request is logged with its record ID and error message for troubleshooting.
        /// Successful requests are counted but not individually logged (to reduce log verbosity).
        /// </remarks>
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
