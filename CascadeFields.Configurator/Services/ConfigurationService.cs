using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CascadeFields.Configurator.Models;
using CascadeFields.Configurator.Models.UI;
using CascadeFields.Configurator.Models.Domain;
using CascadeFields.Plugin.Models;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using Newtonsoft.Json;
using PluginCascadeConfiguration = CascadeFields.Plugin.Models.CascadeConfiguration;

namespace CascadeFields.Configurator.Services
{
    public class ConfigurationService : IConfigurationService
    {
        private readonly IOrganizationService _service;

        public ConfigurationService(IOrganizationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public Task<List<ConfiguredRelationship>> GetExistingConfigurationsAsync()
        {
            return Task.Run(() =>
            {
                var query = new QueryExpression("sdkmessageprocessingstep")
                {
                    ColumnSet = new ColumnSet("sdkmessageprocessingstepid", "name", "configuration"),
                    Criteria = new FilterExpression(LogicalOperator.And)
                };

                query.Criteria.AddCondition("configuration", ConditionOperator.NotNull);

                var pluginTypeLink = query.AddLink("plugintype", "plugintypeid", "plugintypeid", JoinOperator.Inner);
                pluginTypeLink.Columns = new ColumnSet("typename", "name");
                pluginTypeLink.LinkCriteria.AddCondition("typename", ConditionOperator.Equal, "CascadeFields.Plugin.CascadeFieldsPlugin");

                var results = _service.RetrieveMultiple(query);
                var configured = new List<ConfiguredRelationship>();
                var metadataService = new MetadataService(_service);

                foreach (var step in results.Entities)
                {
                    var rawConfig = step.GetAttributeValue<string>("configuration");
                    if (string.IsNullOrWhiteSpace(rawConfig))
                    {
                        continue;
                    }

                    try
                    {
                        var config = JsonConvert.DeserializeObject<PluginCascadeConfiguration>(rawConfig);
                        if (config?.RelatedEntities == null)
                        {
                            continue;
                        }

                        foreach (var related in config.RelatedEntities)
                        {
                            // Resolve friendly display names from metadata
                            string childDisplay = related.EntityName;
                            string lookupDisplay = related.LookupFieldName;
                            try
                            {
                                var childMeta = metadataService.GetEntityMetadataAsync(related.EntityName).Result;
                                childDisplay = childMeta?.DisplayName?.UserLocalizedLabel?.Label ?? related.EntityName;

                                if (!string.IsNullOrWhiteSpace(related.LookupFieldName) && childMeta?.Attributes != null)
                                {
                                    var attr = childMeta.Attributes.FirstOrDefault(a => a.LogicalName == related.LookupFieldName);
                                    lookupDisplay = attr?.DisplayName?.UserLocalizedLabel?.Label ?? related.LookupFieldName;
                                }
                            }
                            catch
                            {
                                // Ignore metadata resolution errors and fall back to logical names
                            }

                            configured.Add(new ConfiguredRelationship
                            {
                                ParentEntity = config.ParentEntity,
                                ChildEntity = related.EntityName,
                                ChildEntityDisplayName = childDisplay,
                                RelationshipName = related.RelationshipName,
                                LookupFieldDisplayName = lookupDisplay,
                                LookupFieldName = related.LookupFieldName,
                                Configuration = config,
                                RawJson = rawConfig
                            });
                        }
                    }
                    catch (JsonException ex)
                    {
                        // Add invalid configuration with error details for troubleshooting
                        configured.Add(new ConfiguredRelationship
                        {
                            ParentEntity = "Unknown",
                            ChildEntity = "Unknown",
                            RelationshipName = $"Invalid configuration: {ex.Message}",
                            RawJson = rawConfig
                        });
                    }
                }

                return configured
                    .OrderBy(c => c.ParentEntity)
                    .ThenBy(c => c.ChildEntity)
                    .ToList();
            });
        }

        public Task<string?> GetConfigurationForParentEntityAsync(string parentEntityLogicalName)
        {
            return Task.Run(async () =>
            {
                var configurations = await GetExistingConfigurationsAsync();
                var config = configurations.FirstOrDefault(c => 
                    c.ParentEntity == parentEntityLogicalName);
                return config?.RawJson;
            });
        }

        public Task PublishConfigurationAsync(CascadeConfigurationModel configuration, IProgress<string> progress, CancellationToken cancellationToken, Guid? solutionId = null)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress.Report("Validating configuration...");
                if (configuration == null) throw new ArgumentNullException(nameof(configuration));

                // If no fields are marked as triggers, mark all fields as triggers
                var allMappings = configuration.RelatedEntities?.SelectMany(r => r.FieldMappings).ToList();
                if (allMappings != null && !allMappings.Any(m => m.IsTriggerField))
                {
                    progress.Report("No trigger fields found - marking all fields as triggers...");
                    foreach (var mapping in allMappings)
                    {
                        mapping.IsTriggerField = true;
                    }
                }

                // Ensure plugin type exists
                var pluginType = FindOrCreatePluginType(progress);
                if (pluginType == null)
                {
                    throw new InvalidPluginExecutionException("CascadeFields plugin type not found. Use 'Update Cascade Fields Plug-in' first.");
                }

                // Ensure sdkmessage 'Update' and filter for entity
                progress.Report("Resolving SDK message and filters...");
                var updateMessageId = GetSdkMessageId("Update");
                var filterId = GetSdkMessageFilterId(updateMessageId, configuration.ParentEntity);

                // Create or update step
                var stepId = UpsertProcessingStep(configuration, pluginType, updateMessageId, filterId, progress);

                // Ensure preimage
                var preImageId = UpsertPreImage(stepId, configuration, progress);

                // Add components to solution if specified
                if (solutionId.HasValue && solutionId != Guid.Empty)
                {
                    progress.Report("Adding components to solution...");
                    AddComponentsToSolution(pluginType, stepId, preImageId, solutionId.Value, progress);
                }

                progress.Report("Publish complete: step and preimage upserted.");
            }, cancellationToken);
        }

        public Task UpdatePluginAssemblyAsync(string assemblyPath, IProgress<string> progress, CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (string.IsNullOrWhiteSpace(assemblyPath) || !System.IO.File.Exists(assemblyPath))
                {
                    throw new InvalidPluginExecutionException($"Assembly not found: {assemblyPath}");
                }

                var assemblyName = "CascadeFields.Plugin";
                var typeName = "CascadeFields.Plugin.CascadeFieldsPlugin";

                progress.Report("Reading assembly bytes...");
                var content = System.Convert.ToBase64String(System.IO.File.ReadAllBytes(assemblyPath));

                // Upsert pluginassembly
                progress.Report("Upserting plugin assembly...");
                var assemblyId = FindPluginAssemblyId(assemblyName);
                var assemblyEntity = new Entity("pluginassembly");
                if (assemblyId.HasValue)
                {
                    assemblyEntity.Id = assemblyId.Value;
                }
                assemblyEntity["name"] = assemblyName;
                assemblyEntity["content"] = content;
                assemblyEntity["isolationmode"] = new OptionSetValue(2); // Sandbox
                assemblyEntity["sourcetype"] = new OptionSetValue(0); // Database

                if (assemblyEntity.Id == Guid.Empty)
                {
                    assemblyEntity.Id = _service.Create(assemblyEntity);
                    progress.Report("Assembly created.");
                }
                else
                {
                    _service.Update(assemblyEntity);
                    progress.Report("Assembly updated.");
                }

                // Upsert plugintype
                progress.Report("Upserting plugin type...");
                var typeId = FindPluginTypeId(typeName);
                var typeEntity = new Entity("plugintype");
                if (typeId.HasValue)
                {
                    typeEntity.Id = typeId.Value;
                }
                typeEntity["pluginassemblyid"] = new EntityReference("pluginassembly", assemblyEntity.Id);
                typeEntity["typename"] = typeName;
                typeEntity["name"] = "CascadeFields Plugin";
                typeEntity["friendlyname"] = "CascadeFields Plugin";

                if (typeEntity.Id == Guid.Empty)
                {
                    typeEntity.Id = _service.Create(typeEntity);
                    progress.Report("Plugin type created.");
                }
                else
                {
                    _service.Update(typeEntity);
                    progress.Report("Plugin type updated.");
                }

                progress.Report("Plugin assembly update complete.");
            }, cancellationToken);
        }

        private Guid? FindPluginAssemblyId(string assemblyName)
        {
            var query = new QueryExpression("pluginassembly")
            {
                ColumnSet = new ColumnSet("pluginassemblyid", "name"),
                Criteria = new FilterExpression()
            };
            query.Criteria.AddCondition("name", ConditionOperator.Equal, assemblyName);
            var results = _service.RetrieveMultiple(query);
            return results.Entities.Count > 0 ? (Guid?)results.Entities[0].Id : null;
        }

        private Guid? FindPluginTypeId(string typeName)
        {
            var query = new QueryExpression("plugintype")
            {
                ColumnSet = new ColumnSet("plugintypeid", "typename"),
                Criteria = new FilterExpression()
            };
            query.Criteria.AddCondition("typename", ConditionOperator.Equal, typeName);
            var results = _service.RetrieveMultiple(query);
            return results.Entities.Count > 0 ? (Guid?)results.Entities[0].Id : null;
        }

        private Entity FindOrCreatePluginType(IProgress<string> progress)
        {
            var typeName = "CascadeFields.Plugin.CascadeFieldsPlugin";
            var query = new QueryExpression("plugintype")
            {
                ColumnSet = new ColumnSet(true)
            };
            query.Criteria.AddCondition("typename", ConditionOperator.Equal, typeName);
            var results = _service.RetrieveMultiple(query);
            return results.Entities.FirstOrDefault();
        }

        private Guid GetSdkMessageId(string name)
        {
            var query = new QueryExpression("sdkmessage")
            {
                ColumnSet = new ColumnSet("sdkmessageid", "name")
            };
            query.Criteria.AddCondition("name", ConditionOperator.Equal, name);
            var results = _service.RetrieveMultiple(query);
            if (!results.Entities.Any()) throw new InvalidPluginExecutionException($"SDK Message '{name}' not found.");
            return results.Entities[0].Id;
        }

        private Guid GetSdkMessageFilterId(Guid messageId, string primaryEntity)
        {
            var query = new QueryExpression("sdkmessagefilter")
            {
                ColumnSet = new ColumnSet("sdkmessagefilterid", "primaryobjecttypecode")
            };
            query.Criteria.AddCondition("sdkmessageid", ConditionOperator.Equal, messageId);
            query.Criteria.AddCondition("primaryobjecttypecode", ConditionOperator.Equal, primaryEntity);
            var results = _service.RetrieveMultiple(query);
            if (!results.Entities.Any()) throw new InvalidPluginExecutionException($"SDK Message Filter for '{primaryEntity}' not found.");
            return results.Entities[0].Id;
        }

        private Guid UpsertProcessingStep(CascadeConfigurationModel config, Entity pluginType, Guid messageId, Guid filterId, IProgress<string> progress)
        {
            var stepName = $"CascadeFields: {config.ParentEntity}";
            var query = new QueryExpression("sdkmessageprocessingstep")
            {
                ColumnSet = new ColumnSet(true)
            };
            query.Criteria.AddCondition("name", ConditionOperator.Equal, stepName);
            query.Criteria.AddCondition("plugintypeid", ConditionOperator.Equal, pluginType.Id);
            var existing = _service.RetrieveMultiple(query).Entities.FirstOrDefault();

            var triggerFields = string.Join(",", config.RelatedEntities?.SelectMany(r => r.FieldMappings)?.Where(m => m.IsTriggerField).Select(m => m.SourceField).Distinct() ?? Enumerable.Empty<string>());

            var step = new Entity("sdkmessageprocessingstep");
            if (existing != null) step.Id = existing.Id;
            step["name"] = stepName;
            step["plugintypeid"] = new EntityReference("plugintype", pluginType.Id);
            step["sdkmessageid"] = new EntityReference("sdkmessage", messageId);
            step["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", filterId);
            step["stage"] = new OptionSetValue(40); // Post-operation
            step["mode"] = new OptionSetValue(1); // Async
            step["rank"] = 1;
            step["supporteddeployment"] = new OptionSetValue(0); // Server
            step["configuration"] = JsonConvert.SerializeObject(config);
            if (!string.IsNullOrWhiteSpace(triggerFields)) step["filteringattributes"] = triggerFields;

            Guid id;
            if (step.Id == Guid.Empty)
            {
                id = _service.Create(step);
                progress.Report("Created processing step.");
            }
            else
            {
                _service.Update(step);
                id = step.Id;
                progress.Report("Updated processing step.");
            }

            return id;
        }

        private Guid UpsertPreImage(Guid stepId, CascadeConfigurationModel config, IProgress<string> progress)
        {
            var query = new QueryExpression("sdkmessageprocessingstepimage")
            {
                ColumnSet = new ColumnSet(true)
            };
            query.Criteria.AddCondition("sdkmessageprocessingstepid", ConditionOperator.Equal, stepId);
            query.Criteria.AddCondition("name", ConditionOperator.Equal, "PreImage");
            var existing = _service.RetrieveMultiple(query).Entities.FirstOrDefault();

            // Collect source fields from all field mappings (parent entity attributes only)
            var sourceFields = config.RelatedEntities?
                .SelectMany(r => r.FieldMappings)
                .Select(m => m.SourceField)
                .Distinct()
                .ToList() ?? new List<string>();

            // Note: LookupFieldName is on the child entity, not parent, so don't include it in PreImage
            var attributesString = string.Join(",", sourceFields);

            var image = new Entity("sdkmessageprocessingstepimage");
            Guid id;
            if (existing != null)
            {
                image.Id = existing.Id;
            }
            image["sdkmessageprocessingstepid"] = new EntityReference("sdkmessageprocessingstep", stepId);
            image["name"] = "PreImage";
            image["entityalias"] = "PreImage";
            image["imagetype"] = new OptionSetValue(0); // PreImage
            image["messagepropertyname"] = "Target"; // Required - specifies which message property contains the entity
            image["attributes"] = attributesString; // Only parent entity source fields

            if (image.Id == Guid.Empty)
            {
                id = _service.Create(image);
                progress.Report("Created pre-image.");
            }
            else
            {
                _service.Update(image);
                id = image.Id;
                progress.Report("Updated pre-image.");
            }

            return id;
        }

        private void AddComponentsToSolution(Entity pluginType, Guid stepId, Guid preImageId, Guid solutionId, IProgress<string> progress)
        {
            try
            {
                // Get the plugin assembly ID from the plugin type
                var pluginAssemblyId = pluginType.GetAttributeValue<EntityReference>("pluginassemblyid")?.Id ?? Guid.Empty;
                if (pluginAssemblyId == Guid.Empty)
                {
                    progress.Report("Warning: Could not find plugin assembly. Solution component might be incomplete.");
                    return;
                }

                var componentsAdded = 0;

                // Add plugin assembly component (Component Type = 91)
                if (!ComponentExistsInSolution(solutionId, 91, pluginAssemblyId))
                {
                    AddSolutionComponent(solutionId, 91, pluginAssemblyId);
                    componentsAdded++;
                    progress.Report("Added plugin assembly to solution.");
                }

                // Add plugin type component (Component Type = 90)
                if (!ComponentExistsInSolution(solutionId, 90, pluginType.Id))
                {
                    AddSolutionComponent(solutionId, 90, pluginType.Id);
                    componentsAdded++;
                    progress.Report("Added plugin type to solution.");
                }

                // Add processing step component (Component Type = 92)
                if (!ComponentExistsInSolution(solutionId, 92, stepId))
                {
                    AddSolutionComponent(solutionId, 92, stepId);
                    componentsAdded++;
                    progress.Report("Added processing step to solution.");
                }

                // Add step image component (Component Type = 93)
                if (!ComponentExistsInSolution(solutionId, 93, preImageId))
                {
                    AddSolutionComponent(solutionId, 93, preImageId);
                    componentsAdded++;
                    progress.Report("Added step image to solution.");
                }

                progress.Report($"Solution component assignment complete ({componentsAdded} components added).");
            }
            catch (Exception ex)
            {
                progress.Report($"Warning: Failed to add components to solution: {ex.Message}");
            }
        }

        private bool ComponentExistsInSolution(Guid solutionId, int componentType, Guid componentId)
        {
            try
            {
                var query = new QueryExpression("solutioncomponent")
                {
                    ColumnSet = new ColumnSet("solutioncomponentid")
                };
                query.Criteria.AddCondition("solutionid", ConditionOperator.Equal, solutionId);
                query.Criteria.AddCondition("componenttype", ConditionOperator.Equal, componentType);
                query.Criteria.AddCondition("objectid", ConditionOperator.Equal, componentId);

                var results = _service.RetrieveMultiple(query);
                return results.Entities.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private void AddSolutionComponent(Guid solutionId, int componentType, Guid componentId)
        {
            var component = new Entity("solutioncomponent")
            {
                ["solutionid"] = new EntityReference("solution", solutionId),
                ["componenttype"] = new OptionSetValue(componentType),
                ["objectid"] = componentId
            };

            _service.Create(component);
        }
    }
}
