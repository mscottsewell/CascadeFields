using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using CascadeFields.Configurator.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Metadata.Query;
using Microsoft.Xrm.Sdk.Query;

namespace CascadeFields.Configurator.Services
{
    internal class MetadataService
    {
        private readonly IOrganizationService _service;

        public MetadataService(IOrganizationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

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
                return results.Entities
                    .Select(e => new SolutionItem
                    {
                        Id = e.Id,
                        FriendlyName = e.GetAttributeValue<string>("friendlyname") ?? "(no name)",
                        UniqueName = e.GetAttributeValue<string>("uniquename") ?? string.Empty
                    })
                    .Where(s => !systemSolutions.Contains(s.UniqueName, StringComparer.OrdinalIgnoreCase) &&
                               !s.FriendlyName.Equals("Common Data Service Default Solution", StringComparison.OrdinalIgnoreCase))
                    .ToList();
            });
        }

        public Task<List<EntityMetadata>> GetSolutionEntitiesAsync(string solutionUniqueName)
        {
            if (string.IsNullOrWhiteSpace(solutionUniqueName))
            {
                throw new ArgumentNullException(nameof(solutionUniqueName));
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

                // Retrieve ALL entities in one batch request (no filter), then filter in memory
                // This is much faster than individual requests
                var entityProperties = new MetadataPropertiesExpression
                {
                    AllProperties = false
                };
                entityProperties.PropertyNames.AddRange(new[]
                {
                    "MetadataId", "LogicalName", "DisplayName", "PrimaryIdAttribute", "PrimaryNameAttribute",
                    "IsIntersect", "IsCustomizable", "Attributes", "OneToManyRelationships", "ManyToOneRelationships"
                });

                var entityQueryExpression = new EntityQueryExpression
                {
                    Properties = entityProperties,
                    AttributeQuery = new AttributeQueryExpression
                    {
                        Properties = new MetadataPropertiesExpression
                        {
                            AllProperties = false,
                            PropertyNames = { "LogicalName", "DisplayName", "AttributeType", "IsValidForUpdate", "IsLogical" }
                        }
                    },
                    RelationshipQuery = new RelationshipQueryExpression
                    {
                        Properties = new MetadataPropertiesExpression { AllProperties = true }
                    }
                };

                var retrieveMetadataChangesRequest = new RetrieveMetadataChangesRequest
                {
                    Query = entityQueryExpression,
                    ClientVersionStamp = null
                };

                var metadataResponse = (RetrieveMetadataChangesResponse)_service.Execute(retrieveMetadataChangesRequest);
                
                // Filter to only entities in this solution, excluding intersection and non-customizable entities
                var entities = metadataResponse.EntityMetadata
                    .Where(e => entityIds.Contains(e.MetadataId.GetValueOrDefault()))
                    .Where(e => e.IsIntersect != true && e.IsCustomizable?.Value != false)
                    .ToList();

                return entities;
            });
        }

        public Task<EntityMetadata?> GetEntityMetadataAsync(string logicalName)
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
                    RetrieveAsIfPublished = false
                };

                var response = (RetrieveEntityResponse)_service.Execute(request);
                return response?.EntityMetadata;
            });
        }

        public Task<List<FormItem>> GetMainFormsAsync(string entityLogicalName)
        {
            return Task.Run(() =>
            {
                var query = new QueryExpression("systemform")
                {
                    ColumnSet = new ColumnSet("formid", "name", "formxml"),
                    Criteria = new FilterExpression(LogicalOperator.And),
                    Orders = { new OrderExpression("name", OrderType.Ascending) }
                };

                query.Criteria.AddCondition("type", ConditionOperator.Equal, 2); // Main
                query.Criteria.AddCondition("objecttypecode", ConditionOperator.Equal, entityLogicalName);

                var response = _service.RetrieveMultiple(query);
                return response.Entities
                    .Select(e => new FormItem
                    {
                        Id = e.Id,
                        Name = e.GetAttributeValue<string>("name") ?? "Unnamed form",
                        FormXml = e.GetAttributeValue<string>("formxml") ?? string.Empty
                    })
                    .ToList();
            });
        }

        public IEnumerable<AttributeItem> GetAttributeItems(EntityMetadata entity, HashSet<string>? formFields = null)
        {
            if (entity?.Attributes == null)
            {
                return Enumerable.Empty<AttributeItem>();
            }

            var attributes = entity.Attributes
                .Where(a => a.IsValidForUpdate == true && !a.IsLogical.GetValueOrDefault())
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

        public IEnumerable<RelationshipItem> GetChildRelationships(EntityMetadata parent, IReadOnlyCollection<EntityMetadata> allEntities)
        {
            if (parent?.OneToManyRelationships == null)
            {
                return Enumerable.Empty<RelationshipItem>();
            }

            // Only include relationships where the referencing entity exists in the current solution set.
            // DO NOT group - we want all relationships shown, even if multiple exist from same child entity
            var relationships = parent.OneToManyRelationships
                .Where(r => string.Equals(r.ReferencedEntity, parent.LogicalName, StringComparison.OrdinalIgnoreCase))
                .Where(r => !string.IsNullOrWhiteSpace(r.ReferencingEntity) && !string.IsNullOrWhiteSpace(r.ReferencingAttribute))
                .Select(r => new
                {
                    Relationship = r,
                    Child = allEntities.FirstOrDefault(e => string.Equals(e.LogicalName, r.ReferencingEntity, StringComparison.OrdinalIgnoreCase))
                })
                .Where(x => x.Child != null)
                .Select(x =>
                {
                    var childDisplayName = x.Child!.DisplayName?.UserLocalizedLabel?.Label
                                         ?? x.Relationship.ReferencingEntity
                                         ?? string.Empty;
                    return new RelationshipItem
                    {
                        SchemaName = x.Relationship.SchemaName,
                        ReferencingEntity = x.Relationship.ReferencingEntity ?? string.Empty,
                        ReferencingAttribute = x.Relationship.ReferencingAttribute ?? string.Empty,
                        DisplayName = childDisplayName
                    };
                })
                .OrderBy(r => r.DisplayName)
                .ThenBy(r => r.ReferencingAttribute)
                .ToList();

            return relationships;
        }

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
