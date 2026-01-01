using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using CascadeFields.Configurator.Models;
using CascadeFields.Configurator.Models.UI;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Microsoft.Xrm.Sdk.Query;

namespace CascadeFields.Configurator.Services
{
    /// <summary>
    /// Provides Dataverse metadata queries used by the configurator UI for solutions, entities, attributes, and relationships.
    /// </summary>
    public class MetadataService : IMetadataService
    {
        private readonly IOrganizationService _service;
        private readonly ConcurrentDictionary<string, List<EntityMetadata>> _solutionEntitiesCache = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, List<RelationshipItem>> _childRelationshipCache = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Initializes a metadata service bound to a specific organization service.
        /// </summary>
        public MetadataService(IOrganizationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        /// <summary>
        /// Returns unmanaged, visible solutions, with the Default solution pinned to the top for convenience.
        /// </summary>
        public Task<List<SolutionItem>> GetUnmanagedSolutionsAsync()
        {
            return Task.Run(() =>
            {
                var query = new QueryExpression("solution")
                {
                    ColumnSet = new ColumnSet("solutionid", "friendlyname", "uniquename", "ismanaged", "isvisible"),
                    Orders = { new OrderExpression("friendlyname", OrderType.Ascending) }
                };

                query.Criteria.AddCondition("ismanaged", ConditionOperator.Equal, false);
                query.Criteria.AddCondition("isvisible", ConditionOperator.Equal, true);

                var results = _service.RetrieveMultiple(query);
                var systemSolutions = new[] { "Active", "Basic", "Common" };
                var solutions = results.Entities
                    .Select(e => new SolutionItem
                    {
                        Id = e.Id,
                        FriendlyName = e.GetAttributeValue<string>("friendlyname") ?? "(no name)",
                        UniqueName = e.GetAttributeValue<string>("uniquename") ?? string.Empty
                    })
                    .Where(s => !systemSolutions.Contains(s.UniqueName, StringComparer.OrdinalIgnoreCase) &&
                               !s.FriendlyName.Equals("Common Data Service Default Solution", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                
                // Ensure "Default" solution is at the top of the list
                var defaultSolution = solutions.FirstOrDefault(s => s.UniqueName.Equals("Default", StringComparison.OrdinalIgnoreCase));
                if (defaultSolution != null)
                {
                    solutions.Remove(defaultSolution);
                    solutions.Insert(0, defaultSolution);
                }
                
                return solutions;
            });
        }

        /// <summary>
        /// Retrieves entity metadata for entities that belong to a specific solution, using MetadataChanges for performance.
        /// </summary>
        public Task<List<EntityMetadata>> GetSolutionEntitiesAsync(string solutionUniqueName, IProgress<MetadataLoadProgress>? progress = null)
        {
            if (string.IsNullOrWhiteSpace(solutionUniqueName))
            {
                throw new ArgumentNullException(nameof(solutionUniqueName));
            }

            // Cache solution entity sets to avoid repeated metadata round-trips
            if (_solutionEntitiesCache.TryGetValue(solutionUniqueName, out var cached))
            {
                return Task.FromResult(cached);
            }

            return Task.Run(() =>
            {
                // Resolve solution id from unique name
                var solutionQuery = new QueryExpression("solution")
                {
                    ColumnSet = new ColumnSet("solutionid"),
                    Criteria =
                    {
                        Conditions =
                        {
                            new ConditionExpression("uniquename", ConditionOperator.Equal, solutionUniqueName)
                        }
                    }
                };

                var solution = _service.RetrieveMultiple(solutionQuery).Entities.FirstOrDefault();
                var solutionId = solution?.Id ?? Guid.Empty;
                if (solutionId == Guid.Empty)
                {
                    return new List<EntityMetadata>();
                }

                // Get entity component ids from the solution
                var componentQuery = new QueryExpression("solutioncomponent")
                {
                    ColumnSet = new ColumnSet("objectid"),
                    Criteria =
                    {
                        Conditions =
                        {
                            new ConditionExpression("solutionid", ConditionOperator.Equal, solutionId),
                            new ConditionExpression("componenttype", ConditionOperator.Equal, 1) // Entity
                        }
                    }
                };

                var components = _service.RetrieveMultiple(componentQuery);
                var entityIds = new HashSet<Guid>(components.Entities
                    .Select(e => e.GetAttributeValue<Guid>("objectid"))
                    .Where(id => id != Guid.Empty));

                if (entityIds.Count == 0)
                {
                    progress?.Report(new MetadataLoadProgress(0, 0, "No entities found in solution."));
                    return new List<EntityMetadata>();
                }

                progress?.Report(new MetadataLoadProgress(0, entityIds.Count, "Retrieving entity metadata..."));

                // OPTIMIZED: Use EntityFilters and MetadataConditions to only retrieve entities in this solution
                // This is MUCH faster than retrieving all entities and filtering in memory
                var entityProperties = new MetadataPropertiesExpression
                {
                    AllProperties = false
                };
                entityProperties.PropertyNames.AddRange(new[]
                {
                    "MetadataId", "LogicalName", "DisplayName", "PrimaryIdAttribute", "PrimaryNameAttribute",
                    "IsIntersect", "IsCustomizable", "Attributes", "OneToManyRelationships", "ManyToOneRelationships"
                });

                // Only request attributes that are needed: valid for update, not logical
                var attributeProperties = new MetadataPropertiesExpression
                {
                    AllProperties = false
                };
                attributeProperties.PropertyNames.AddRange(new[]
                {
                    "LogicalName", "DisplayName", "AttributeType", "IsValidForUpdate", "IsLogical", 
                    "AttributeTypeName", "IsValidForCreate", "RequiredLevel"
                });

                // Only request essential relationship properties - not all properties
                var relationshipProperties = new MetadataPropertiesExpression
                {
                    AllProperties = false
                };
                relationshipProperties.PropertyNames.AddRange(new[]
                {
                    "SchemaName", "ReferencedEntity", "ReferencedAttribute", "ReferencingEntity", 
                    "ReferencingAttribute", "RelationshipType"
                });

                // Build criteria to filter only entities in this solution
                var entityFilter = new MetadataFilterExpression(LogicalOperator.And);
                entityFilter.Conditions.Add(new MetadataConditionExpression("MetadataId", MetadataConditionOperator.In, entityIds.ToArray()));
                entityFilter.Conditions.Add(new MetadataConditionExpression("IsIntersect", MetadataConditionOperator.Equals, false));
                // Include managed entities as well; filtering by IsCustomizable would exclude managed solutions when switching environments

                var entityQueryExpression = new EntityQueryExpression
                {
                    Criteria = entityFilter,
                    Properties = entityProperties,
                    AttributeQuery = new AttributeQueryExpression
                    {
                        Properties = attributeProperties
                    },
                    RelationshipQuery = new RelationshipQueryExpression
                    {
                        Properties = relationshipProperties
                    }
                };

                var retrieveMetadataChangesRequest = new RetrieveMetadataChangesRequest
                {
                    Query = entityQueryExpression,
                    ClientVersionStamp = null
                };

                var metadataResponse = (RetrieveMetadataChangesResponse)_service.Execute(retrieveMetadataChangesRequest);
                
                // Entities are already filtered by the query - no need for additional filtering
                var entities = metadataResponse.EntityMetadata.ToList();
                progress?.Report(new MetadataLoadProgress(entityIds.Count, entityIds.Count, "Entity metadata retrieved."));
                _solutionEntitiesCache[solutionUniqueName] = entities;

                return entities;
            });
        }

        public Task<int> GetSolutionEntityCountAsync(string solutionUniqueName)
        {
            if (string.IsNullOrWhiteSpace(solutionUniqueName))
            {
                throw new ArgumentNullException(nameof(solutionUniqueName));
            }

            if (_solutionEntitiesCache.TryGetValue(solutionUniqueName, out var cached))
            {
                return Task.FromResult(cached.Count);
            }

            return Task.Run(() =>
            {
                var solutionQuery = new QueryExpression("solution")
                {
                    ColumnSet = new ColumnSet("solutionid"),
                    Criteria =
                    {
                        Conditions =
                        {
                            new ConditionExpression("uniquename", ConditionOperator.Equal, solutionUniqueName)
                        }
                    }
                };

                var solution = _service.RetrieveMultiple(solutionQuery).Entities.FirstOrDefault();
                var solutionId = solution?.Id ?? Guid.Empty;
                if (solutionId == Guid.Empty)
                {
                    return 0;
                }

                var componentQuery = new QueryExpression("solutioncomponent")
                {
                    ColumnSet = new ColumnSet("objectid"),
                    Criteria =
                    {
                        Conditions =
                        {
                            new ConditionExpression("solutionid", ConditionOperator.Equal, solutionId),
                            new ConditionExpression("componenttype", ConditionOperator.Equal, 1)
                        }
                    }
                };

                var components = _service.RetrieveMultiple(componentQuery);
                var entityIds = new HashSet<Guid>(components.Entities
                    .Select(e => e.GetAttributeValue<Guid>("objectid"))
                    .Where(id => id != Guid.Empty));

                return entityIds.Count;
            });
        }

        /// <summary>
        /// Retrieves full entity metadata (entity/attributes/relationships) for a single logical name.
        /// </summary>
        public Task<EntityMetadata> GetEntityMetadataAsync(string logicalName)
        {
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                throw new ArgumentNullException(nameof(logicalName));
            }

            return Task.Run(() =>
            {
                var request = new RetrieveEntityRequest
                {
                    LogicalName = logicalName,
                    EntityFilters = EntityFilters.Entity | EntityFilters.Attributes | EntityFilters.Relationships,
                    // Use published metadata to ensure all attributes (including ones not in the current solution) are returned
                    RetrieveAsIfPublished = true
                };

                var response = (RetrieveEntityResponse)_service.Execute(request);
                return response!.EntityMetadata;
            });
        }

        /// <summary>
        /// Returns updatable attributes for an entity as simplified items for UI binding.
        /// </summary>
        public Task<List<AttributeItem>> GetAttributesAsync(string entityLogicalName, bool includeReadOnly = false, bool includeLogical = false)
        {
            return Task.Run(async () =>
            {
                var metadata = await GetEntityMetadataAsync(entityLogicalName);
                if (metadata == null)
                    return new List<AttributeItem>();

                return GetAttributeItems(metadata, null, includeReadOnly, includeLogical).ToList();
            });
        }

        /// <summary>
        /// Resolves child relationships for a parent entity using the specified solution (or Active as default) as the entity set.
        /// </summary>
        public Task<List<RelationshipItem>> GetChildRelationshipsAsync(string parentEntityLogicalName, string? solutionUniqueName = null)
        {
            return Task.Run(async () =>
            {
                var cacheKey = $"{solutionUniqueName ?? "Active"}|{parentEntityLogicalName}";
                if (_childRelationshipCache.TryGetValue(cacheKey, out var cached))
                {
                    return cached;
                }

                var metadata = await GetEntityMetadataAsync(parentEntityLogicalName);
                if (metadata == null)
                    return new List<RelationshipItem>();

                // Get only entities from the chosen solution; if none, return empty
                var solutionName = string.IsNullOrWhiteSpace(solutionUniqueName) ? "Active" : solutionUniqueName;
                var allEntities = (await GetSolutionEntitiesAsync(solutionName ?? "Active")).ToList();
                if (allEntities.Count == 0)
                    return new List<RelationshipItem>();

                var relationships = GetChildRelationships(metadata, allEntities).ToList();
                _childRelationshipCache[cacheKey] = relationships;
                return relationships;
            });
        }

        /// <summary>
        /// Builds a collection of attribute items from entity metadata, with optional filtering by form fields.
        /// </summary>
        /// <param name="entity">The entity metadata containing attributes to convert.</param>
        /// <param name="formFields">Optional set of form field logical names to filter by. If null or empty, all attributes are included.</param>
        /// <param name="includeReadOnly">If true, includes read-only attributes. Default is false.</param>
        /// <param name="includeLogical">If true, includes logical attributes (calculated, rollup). Default is false.</param>
        /// <returns>An enumerable collection of attribute items sorted by display name.</returns>
        /// <remarks>
        /// <para><b>Filtering Logic:</b></para>
        /// <list type="number">
        ///     <item><description>Excludes logical attributes unless includeLogical is true</description></item>
        ///     <item><description>Excludes read-only attributes (IsValidForUpdate=false) unless includeReadOnly is true</description></item>
        ///     <item><description>If formFields is provided, only includes attributes in that set</description></item>
        /// </list>
        ///
        /// <para><b>Usage:</b></para>
        /// Used to populate field selection controls with form-specific attributes or all writable attributes.
        /// </remarks>
        public IEnumerable<AttributeItem> GetAttributeItems(EntityMetadata entity, HashSet<string>? formFields = null, bool includeReadOnly = false, bool includeLogical = false)
        {
            if (entity?.Attributes == null)
            {
                return Enumerable.Empty<AttributeItem>();
            }

            var attributes = entity.Attributes
                .Where(a => includeLogical || !a.IsLogical.GetValueOrDefault())
                .Where(a => includeReadOnly || a.IsValidForUpdate == true)
                .Where(a => formFields == null || formFields.Count == 0 || formFields.Contains(a.LogicalName))
                .Select(a => new AttributeItem
                {
                    LogicalName = a.LogicalName,
                    DisplayName = a.DisplayName?.UserLocalizedLabel?.Label ?? a.LogicalName,
                    Metadata = a
                })
                .OrderBy(a => a.DisplayName)
                .ToList();

            return attributes;
        }

        /// <summary>
        /// Builds attribute items specifically for filter criteria selection, always including state and status fields.
        /// </summary>
        /// <param name="entity">The entity metadata containing attributes to convert.</param>
        /// <param name="formFields">Optional set of form field logical names. State and status are always included regardless.</param>
        /// <returns>An enumerable collection of attribute items suitable for filter criteria, sorted by display name.</returns>
        /// <remarks>
        /// <para><b>Special Handling:</b></para>
        /// Unlike <see cref="GetAttributeItems"/>, this method always includes statecode and statuscode
        /// attributes even if they're not in the formFields set. This ensures users can always filter by record state.
        ///
        /// <para><b>Filtering Logic:</b></para>
        /// <list type="bullet">
        ///     <item><description>Only includes attributes valid for update</description></item>
        ///     <item><description>Excludes logical attributes</description></item>
        ///     <item><description>Always includes statecode and statuscode</description></item>
        ///     <item><description>If formFields provided, includes attributes in that set plus state/status</description></item>
        /// </list>
        ///
        /// <para><b>Usage:</b></para>
        /// Used to populate filter field dropdowns in filter criteria builders, ensuring common filter
        /// fields like state/status are always available.
        /// </remarks>
        public IEnumerable<AttributeItem> GetFilterAttributeItems(EntityMetadata entity, HashSet<string>? formFields = null)
        {
            if (entity?.Attributes == null)
            {
                return Enumerable.Empty<AttributeItem>();
            }

            var attributes = entity.Attributes
                .Where(a => a.IsValidForUpdate == true && !a.IsLogical.GetValueOrDefault())
                .Where(a => formFields == null || formFields.Count == 0 || formFields.Contains(a.LogicalName) || 
                           a.LogicalName == "statecode" || a.LogicalName == "statuscode")
                .Select(a => new AttributeItem
                {
                    LogicalName = a.LogicalName,
                    DisplayName = a.DisplayName?.UserLocalizedLabel?.Label ?? a.LogicalName,
                    Metadata = a
                })
                .OrderBy(a => a.DisplayName)
                .ToList();

            return attributes;
        }

        /// <summary>
        /// Builds a list of child relationship items for display, resolving friendly names from metadata.
        /// </summary>
        /// <param name="parent">The parent entity metadata containing OneToManyRelationships.</param>
        /// <param name="allEntities">The collection of all available entities (typically from a solution) to resolve child entity metadata.</param>
        /// <returns>An enumerable collection of relationship items sorted by display name and referencing attribute.</returns>
        /// <remarks>
        /// <para><b>Filtering Logic:</b></para>
        /// <list type="bullet">
        ///     <item><description>Only includes one-to-many relationships where this entity is the parent</description></item>
        ///     <item><description>Filters to relationships where the child entity exists in allEntities</description></item>
        ///     <item><description>Resolves display names from child entity and attribute metadata</description></item>
        /// </list>
        ///
        /// <para><b>Display Name Resolution:</b></para>
        /// For each relationship, attempts to resolve:
        /// <list type="bullet">
        ///     <item><description>Child entity display name from metadata</description></item>
        ///     <item><description>Lookup field display name from child entity attribute metadata</description></item>
        /// </list>
        /// Falls back to logical names if display names are not available.
        ///
        /// <para><b>No Grouping:</b></para>
        /// Intentionally does not group relationships. If multiple relationships exist between the same
        /// two entities (e.g., Account -> Contact via multiple lookup fields), all are returned separately.
        ///
        /// <para><b>Usage:</b></para>
        /// Used to populate relationship picker dialogs with all possible parent-child relationships.
        /// </remarks>
        public IEnumerable<RelationshipItem> GetChildRelationships(EntityMetadata parent, IReadOnlyCollection<EntityMetadata> allEntities)
        {
            if (parent?.OneToManyRelationships == null)
            {
                return Enumerable.Empty<RelationshipItem>();
            }


            // Only include relationships where the referencing entity exists in the current solution set.
            // The relationship component itself may not be in the solution, but the entity must be.
            // DO NOT group - we want all relationships shown, even if multiple exist from the same child entity
                var relationships = parent.OneToManyRelationships
                        .Where(r => string.Equals(r.ReferencedEntity, parent.LogicalName, StringComparison.OrdinalIgnoreCase))
                        .Where(r => !string.IsNullOrWhiteSpace(r.ReferencingEntity) && !string.IsNullOrWhiteSpace(r.ReferencingAttribute))
                        .Select(r => new
                        {
                            Relationship = r,
                            ChildMeta = allEntities.FirstOrDefault(e => string.Equals(e.LogicalName, r.ReferencingEntity, StringComparison.OrdinalIgnoreCase))
                        })
                        .Where(x => x.ChildMeta != null)
                        .Select(x =>
                        {
                            var r = x.Relationship;
                            var childMeta = x.ChildMeta!;

                            var childDisplayName = childMeta.DisplayName?.UserLocalizedLabel?.Label
                                                 ?? r.ReferencingEntity
                                                 ?? string.Empty;

                            var lookupFieldDisplayName = r.ReferencingAttribute ?? string.Empty;
                            if (childMeta.Attributes != null)
                            {
                                var lookupAttr = childMeta.Attributes.FirstOrDefault(a =>
                                    string.Equals(a.LogicalName, r.ReferencingAttribute, StringComparison.OrdinalIgnoreCase));
                                if (lookupAttr != null)
                                {
                                    lookupFieldDisplayName = lookupAttr.DisplayName?.UserLocalizedLabel?.Label
                                                          ?? r.ReferencingAttribute
                                                          ?? string.Empty;
                                }
                            }

                            return new RelationshipItem
                            {
                                SchemaName = r.SchemaName,
                                ReferencingEntity = r.ReferencingEntity ?? string.Empty,
                                ReferencingAttribute = r.ReferencingAttribute ?? string.Empty,
                                DisplayName = childDisplayName,
                                ChildEntityDisplayName = childDisplayName,
                                LookupFieldDisplayName = lookupFieldDisplayName
                            };
                        })
                        .OrderBy(r => r.DisplayName)
                        .ThenBy(r => r.ReferencingAttribute)
                        .ToList();

            return relationships;
        }

        /// <summary>
        /// Parses Dataverse form XML to extract the logical names of all bound fields.
        /// </summary>
        /// <param name="formXml">The XML content of a Dataverse form definition.</param>
        /// <returns>A case-insensitive hash set of field logical names found in the form.</returns>
        /// <remarks>
        /// <para><b>Purpose:</b></para>
        /// Extracts field logical names from form XML to filter attribute lists to only show fields
        /// that are visible on the form. This helps users focus on relevant fields when building
        /// cascade configurations.
        ///
        /// <para><b>Parsing Logic:</b></para>
        /// Searches for all control elements with a "datafieldname" attribute and extracts the value.
        /// Handles malformed XML gracefully by returning an empty set on parse errors.
        ///
        /// <para><b>Usage:</b></para>
        /// Used to limit field selection dropdowns to form-specific fields, improving usability
        /// by hiding irrelevant system or hidden fields.
        ///
        /// <para><b>Error Handling:</b></para>
        /// Returns an empty hash set if formXml is null, empty, or malformed. Does not throw exceptions.
        /// </remarks>
        public HashSet<string> GetFieldsFromFormXml(string formXml)
        {
            var fields = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(formXml))
            {
                return fields;
            }

            try
            {
                var xdoc = XDocument.Parse(formXml);
                var controls = xdoc.Descendants().Where(x => x.Name.LocalName.Equals("control", StringComparison.OrdinalIgnoreCase));
                foreach (var control in controls)
                {
                    var logicalName = control.Attribute("datafieldname")?.Value;
                    if (!string.IsNullOrWhiteSpace(logicalName))
                    {
                        fields.Add(logicalName!);
                    }
                }
            }
            catch
            {
                // Ignore malformed form XML
            }

            return fields;
        }
    }
}
