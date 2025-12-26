using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;

namespace CascadeFields.Configurator.Services
{
    public class MetadataService
    {
        private readonly IOrganizationService _service;
        private readonly Dictionary<string, string> _entityDisplayNameCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public MetadataService(IOrganizationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public string GetEntityDisplayName(string logicalName)
        {
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                return string.Empty;
            }

            if (_entityDisplayNameCache.TryGetValue(logicalName, out var cached))
            {
                return cached;
            }

            var metadata = RetrieveEntity(logicalName, EntityFilters.Entity);
            var display = metadata.DisplayName?.UserLocalizedLabel?.Label ?? logicalName;
            if (string.IsNullOrWhiteSpace(display))
            {
                display = logicalName;
            }

            _entityDisplayNameCache[logicalName] = display;
            return display;
        }

        public List<SolutionOption> GetUnmanagedSolutions()
        {
            var query = new QueryExpression("solution")
            {
                ColumnSet = new ColumnSet("friendlyname", "uniquename", "solutionid"),
                Criteria = new FilterExpression(LogicalOperator.And)
            };

            query.Criteria.AddCondition("ismanaged", ConditionOperator.Equal, false);
            query.AddOrder("friendlyname", OrderType.Ascending);

            var results = _service.RetrieveMultiple(query);

            return results.Entities.Select(e => new SolutionOption
            {
                Id = e.Id,
                FriendlyName = e.GetAttributeValue<string>("friendlyname"),
                UniqueName = e.GetAttributeValue<string>("uniquename")
            }).ToList();
        }

        public List<EntityOption> GetEntitiesForSolution(Guid solutionId)
        {
            const int EntityComponentType = 1;
            var componentQuery = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid"),
                Criteria = new FilterExpression(LogicalOperator.And)
            };

            componentQuery.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);
            componentQuery.Criteria.AddCondition("componenttype", ConditionOperator.Equal, EntityComponentType);

            var componentResults = _service.RetrieveMultiple(componentQuery);

            var entityIds = componentResults.Entities
                .Select(e => e.GetAttributeValue<Guid>("objectid"))
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            var entities = new List<EntityOption>();

            foreach (var metadataId in entityIds)
            {
                try
                {
                    var request = new RetrieveEntityRequest
                    {
                        MetadataId = metadataId,
                        EntityFilters = EntityFilters.Entity,
                        RetrieveAsIfPublished = true
                    };

                    var response = (RetrieveEntityResponse)_service.Execute(request);
                    var metadata = response.EntityMetadata;
                    entities.Add(new EntityOption
                    {
                        MetadataId = metadata.MetadataId ?? Guid.Empty,
                        LogicalName = metadata.LogicalName,
                        DisplayName = metadata.DisplayName?.UserLocalizedLabel?.Label ?? metadata.LogicalName
                    });
                }
                catch (Exception)
                {
                    // Skip invalid or inaccessible entity metadata IDs
                    // (could be incorrect component type or deleted entity)
                    continue;
                }
            }

            return entities.OrderBy(e => e.DisplayName).ToList();
        }

        public List<ViewOption> GetViewsForEntity(Guid solutionId, string entityLogicalName)
        {
            const int SavedQueryComponentType = 26;
            var componentQuery = new QueryExpression("solutioncomponent")
            {
                ColumnSet = new ColumnSet("objectid"),
                Criteria = new FilterExpression(LogicalOperator.And)
            };

            componentQuery.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);
            componentQuery.Criteria.AddCondition("componenttype", ConditionOperator.Equal, SavedQueryComponentType);

            var componentResults = _service.RetrieveMultiple(componentQuery);
            var viewIds = componentResults.Entities
                .Select(e => e.GetAttributeValue<Guid>("objectid"))
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (viewIds.Count == 0)
            {
                return new List<ViewOption>();
            }

            var viewQuery = new QueryExpression("savedquery")
            {
                ColumnSet = new ColumnSet("savedqueryid", "name", "fetchxml", "returnedtypecode"),
                Criteria = new FilterExpression(LogicalOperator.And)
            };

            viewQuery.Criteria.AddCondition("savedqueryid", ConditionOperator.In, viewIds.Cast<object>().ToArray());
            viewQuery.Criteria.AddCondition("returnedtypecode", ConditionOperator.Equal, entityLogicalName);
            viewQuery.Criteria.AddCondition("statecode", ConditionOperator.Equal, 0);

            var viewResults = _service.RetrieveMultiple(viewQuery);

            return viewResults.Entities.Select(v => new ViewOption
            {
                Id = v.Id,
                Name = v.GetAttributeValue<string>("name"),
                FetchXml = v.GetAttributeValue<string>("fetchxml") ?? string.Empty
            })
            .OrderBy(v => v.Name, StringComparer.Create(CultureInfo.CurrentCulture, true))
            .ToList();
        }

        public List<FormOption> GetFormsForEntity(Guid solutionId, string entityLogicalName)
        {
            if (string.IsNullOrWhiteSpace(entityLogicalName))
            {
                return new List<FormOption>();
            }
            
            const int SystemFormComponentType = 60;

            var formIds = new List<Guid>();

            if (solutionId != Guid.Empty)
            {
                var componentQuery = new QueryExpression("solutioncomponent")
                {
                    ColumnSet = new ColumnSet("objectid"),
                    Criteria = new FilterExpression(LogicalOperator.And)
                };

                componentQuery.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);
                componentQuery.Criteria.AddCondition("componenttype", ConditionOperator.Equal, SystemFormComponentType);

                var componentResults = _service.RetrieveMultiple(componentQuery);
                formIds = componentResults.Entities
                    .Select(e => e.GetAttributeValue<Guid>("objectid"))
                    .Where(id => id != Guid.Empty)
                    .Distinct()
                    .ToList();

                if (formIds.Count == 0)
                {
                    return new List<FormOption>();
                }
            }

            var query = new QueryExpression("systemform")
            {
                ColumnSet = new ColumnSet("name", "formid", "type", "formxml"),
                Criteria = new FilterExpression(LogicalOperator.And)
            };

            // Type 2 = Main form
            query.Criteria.AddCondition("type", ConditionOperator.Equal, 2);
            query.Criteria.AddCondition("objecttypecode", ConditionOperator.Equal, entityLogicalName);

            if (formIds.Count > 0)
            {
                query.Criteria.AddCondition("formid", ConditionOperator.In, formIds.Cast<object>().ToArray());
            }

            var results = _service.RetrieveMultiple(query);

            return results.Entities.Select(e =>
            {
                var form = new FormOption
                {
                    Id = e.Id,
                    Name = e.GetAttributeValue<string>("name") ?? string.Empty,
                    Fields = ExtractFieldsFromFormXml(e.GetAttributeValue<string>("formxml") ?? string.Empty)
                };

                return form;
            })
            .OrderBy(f => f.Name, StringComparer.Create(CultureInfo.CurrentCulture, true))
            .ToList();
        }

        public List<LookupFieldOption> GetLookupFields(string entityLogicalName)
        {
            var metadata = RetrieveEntity(entityLogicalName, EntityFilters.Attributes);
            var lookupAttributes = metadata.Attributes
                .OfType<LookupAttributeMetadata>()
                .Where(a => a.IsValidForUpdate == true)
                .ToList();

            // Customer and owner attributes also derive from LookupAttributeMetadata
            return lookupAttributes.Select(a => new LookupFieldOption
            {
                LogicalName = a.LogicalName,
                DisplayName = a.DisplayName?.UserLocalizedLabel?.Label ?? a.LogicalName,
                Targets = a.Targets ?? Array.Empty<string>()
            }).OrderBy(l => l.DisplayName).ToList();
        }

        public (List<AttributeOption> parentAttributes, List<AttributeOption> childAttributes) GetAttributeOptions(string parentLogicalName, string childLogicalName)
        {
            if (string.IsNullOrWhiteSpace(parentLogicalName) || string.IsNullOrWhiteSpace(childLogicalName))
            {
                return (new List<AttributeOption>(), new List<AttributeOption>());
            }
            
            var parent = RetrieveEntity(parentLogicalName, EntityFilters.Attributes);
            var child = RetrieveEntity(childLogicalName, EntityFilters.Attributes);

            var parentOptions = parent.Attributes
                .Where(IsEligibleAttribute)
                .Select(ToAttributeOption)
                .OrderBy(a => a.DisplayName)
                .ToList();

            var childOptions = child.Attributes
                .Where(IsEligibleAttribute)
                .Select(ToAttributeOption)
                .OrderBy(a => a.DisplayName)
                .ToList();

            return (parentOptions, childOptions);
        }

        private EntityMetadata RetrieveEntity(string logicalName, EntityFilters filters)
        {
            if (string.IsNullOrWhiteSpace(logicalName))
            {
                throw new ArgumentException("Entity logical name cannot be null or empty", nameof(logicalName));
            }
            
            var request = new RetrieveEntityRequest
            {
                LogicalName = logicalName,
                EntityFilters = filters,
                RetrieveAsIfPublished = true
            };

            var response = (RetrieveEntityResponse)_service.Execute(request);
            return response.EntityMetadata;
        }

        private bool IsEligibleAttribute(AttributeMetadata attribute)
        {
            if (attribute == null)
            {
                return false;
            }

            if (attribute.IsValidForUpdate != true)
            {
                return false;
            }

            if (attribute.AttributeOf != null)
            {
                return false;
            }

            switch (attribute.AttributeType)
            {
                case AttributeTypeCode.Virtual:
                case AttributeTypeCode.Uniqueidentifier:
                case AttributeTypeCode.CalendarRules:
                case AttributeTypeCode.ManagedProperty:
                    return false;
                default:
                    return true;
            }
        }

        private AttributeOption ToAttributeOption(AttributeMetadata attribute)
        {
            var targets = Array.Empty<string>();
            if (attribute is LookupAttributeMetadata lookupMeta && lookupMeta.Targets != null)
            {
                targets = lookupMeta.Targets;
            }

            return new AttributeOption
            {
                LogicalName = attribute.LogicalName,
                DisplayName = attribute.DisplayName?.UserLocalizedLabel?.Label ?? attribute.LogicalName,
                AttributeType = attribute.AttributeType,
                Format = attribute.AttributeTypeName?.Value ?? string.Empty,
                Targets = targets
            };
        }

        private HashSet<string> ExtractFieldsFromFormXml(string formXml)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(formXml))
            {
                return set;
            }

            try
            {
                var doc = XDocument.Parse(formXml);
                // Form XML uses a default namespace, so match on LocalName rather than the full name
                var controls = doc.Descendants()
                    .Where(e => string.Equals(e.Name.LocalName, "control", StringComparison.OrdinalIgnoreCase))
                    .Select(c => c.Attribute("datafieldname")?.Value)
                    .Where(v => !string.IsNullOrWhiteSpace(v));

                foreach (var field in controls)
                {
                    set.Add(field!);
                }
            }
            catch
            {
                // Ignore malformed form xml
            }

            return set;
        }
    }
}
