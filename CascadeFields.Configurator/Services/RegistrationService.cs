using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using CascadeFields.Plugin.Models;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace CascadeFields.Configurator.Services
{
    public class RegistrationService
    {
        private readonly IOrganizationService _service;

        public RegistrationService(IOrganizationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
        }

        public Guid EnsurePluginAssembly(string assemblyPath, string solutionUniqueName)
        {
            if (!File.Exists(assemblyPath))
            {
                throw new FileNotFoundException("Plugin assembly not found", assemblyPath);
            }

            var name = AssemblyName.GetAssemblyName(assemblyPath);
            var assemblyBytes = File.ReadAllBytes(assemblyPath);
            var base64 = Convert.ToBase64String(assemblyBytes);
            var tokenBytes = name.GetPublicKeyToken() ?? Array.Empty<byte>();
            var publicKeyToken = BitConverter.ToString(tokenBytes).Replace("-", string.Empty).ToLowerInvariant();
            var culture = string.IsNullOrWhiteSpace(name.CultureName) ? "neutral" : name.CultureName;
            var version = name.Version?.ToString() ?? "1.0.0.0";

            var query = new QueryExpression("pluginassembly")
            {
                ColumnSet = new ColumnSet("pluginassemblyid", "version", "content"),
                Criteria = new FilterExpression(LogicalOperator.And)
            };

            query.Criteria.AddCondition("name", ConditionOperator.Equal, name.Name);

            var existing = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
            Entity assembly;
            bool isNew = existing == null;

            if (isNew)
            {
                assembly = new Entity("pluginassembly")
                {
                    ["name"] = name.Name,
                    ["culture"] = culture,
                    ["version"] = version,
                    ["publickeytoken"] = publicKeyToken,
                    ["sourcetype"] = new OptionSetValue(0),
                    ["isolationmode"] = new OptionSetValue(1),
                    ["content"] = base64
                };

                var id = _service.Create(assembly);
                assembly.Id = id;
                AddToSolution(solutionUniqueName, id, 91);
            }
            else
            {
                assembly = existing!;
                var update = new Entity("pluginassembly", assembly.Id)
                {
                    ["content"] = base64,
                    ["version"] = version,
                    ["publickeytoken"] = publicKeyToken,
                    ["culture"] = culture
                };

                _service.Update(update);
                AddToSolution(solutionUniqueName, assembly.Id, 91);
            }

            return assembly.Id;
        }

        public Guid EnsurePluginType(Guid assemblyId, string typeName, string friendlyName, string solutionUniqueName)
        {
            var query = new QueryExpression("plugintype")
            {
                ColumnSet = new ColumnSet(true),
                Criteria = new FilterExpression(LogicalOperator.And)
            };

            query.Criteria.AddCondition("typename", ConditionOperator.Equal, typeName);
            query.Criteria.AddCondition("pluginassemblyid", ConditionOperator.Equal, assemblyId);

            var existing = _service.RetrieveMultiple(query).Entities.FirstOrDefault();

            if (existing != null)
            {
                AddToSolution(solutionUniqueName, existing.Id, 90);
                return existing.Id;
            }

            var pluginType = new Entity("plugintype")
            {
                ["pluginassemblyid"] = new EntityReference("pluginassembly", assemblyId),
                ["typename"] = typeName,
                ["friendlyname"] = friendlyName,
                ["name"] = friendlyName
            };

            var id = _service.Create(pluginType);
            AddToSolution(solutionUniqueName, id, 90);
            return id;
        }

        public Guid EnsureStep(Guid pluginTypeId, CascadeConfiguration config, string solutionUniqueName, IEnumerable<string> triggerFields)
        {
            var updateMessageId = GetMessageId("Update");
            var messageFilterId = GetMessageFilterId(updateMessageId, config.ParentEntity);
            var triggerList = string.Join(",", triggerFields.Distinct());

            var stepQuery = new QueryExpression("sdkmessageprocessingstep")
            {
                ColumnSet = new ColumnSet(true),
                Criteria = new FilterExpression(LogicalOperator.And)
            };

            stepQuery.Criteria.AddCondition("plugintypeid", ConditionOperator.Equal, pluginTypeId);
            stepQuery.Criteria.AddCondition("sdkmessageid", ConditionOperator.Equal, updateMessageId);
            stepQuery.Criteria.AddCondition("primaryobjecttypecode", ConditionOperator.Equal, config.ParentEntity);

            var existing = _service.RetrieveMultiple(stepQuery).Entities.FirstOrDefault();
            Entity step;
            bool isNew = existing == null;

            if (isNew)
            {
                step = new Entity("sdkmessageprocessingstep")
                {
                    ["name"] = $"CascadeFields: {config.ParentEntity}",
                    ["description"] = "Cascade field values from parent to related records",
                    ["sdkmessageid"] = new EntityReference("sdkmessage", updateMessageId),
                    ["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", messageFilterId),
                    ["plugintypeid"] = new EntityReference("plugintype", pluginTypeId),
                    ["primaryobjecttypecode"] = config.ParentEntity,
                    ["mode"] = new OptionSetValue(1),
                    ["stage"] = new OptionSetValue(40),
                    ["supporteddeployment"] = new OptionSetValue(0),
                    ["filteringattributes"] = triggerList,
                    ["configuration"] = Newtonsoft.Json.JsonConvert.SerializeObject(config),
                    ["rank"] = 1
                };

                var id = _service.Create(step);
                step.Id = id;
                AddToSolution(solutionUniqueName, id, 92);
                EnsurePreImage(step.Id, config, solutionUniqueName);
                return id;
            }

            step = new Entity("sdkmessageprocessingstep", existing!.Id)
            {
                ["filteringattributes"] = triggerList,
                ["configuration"] = Newtonsoft.Json.JsonConvert.SerializeObject(config),
                ["sdkmessagefilterid"] = new EntityReference("sdkmessagefilter", messageFilterId),
                ["primaryobjecttypecode"] = config.ParentEntity
            };

            _service.Update(step);
            AddToSolution(solutionUniqueName, existing.Id, 92);
            EnsurePreImage(existing.Id, config, solutionUniqueName);
            return existing.Id;
        }

        private void EnsurePreImage(Guid stepId, CascadeConfiguration config, string solutionUniqueName)
        {
            var imageQuery = new QueryExpression("sdkmessageprocessingstepimage")
            {
                ColumnSet = new ColumnSet(true),
                Criteria = new FilterExpression(LogicalOperator.And)
            };

            imageQuery.Criteria.AddCondition("sdkmessageprocessingstepid", ConditionOperator.Equal, stepId);
            imageQuery.Criteria.AddCondition("imagetype", ConditionOperator.Equal, 0);
            imageQuery.Criteria.AddCondition("entityalias", ConditionOperator.Equal, "PreImage");

            var existing = _service.RetrieveMultiple(imageQuery).Entities.FirstOrDefault();

            var attributes = string.Join(",", config.RelatedEntities
                .SelectMany(re => re.FieldMappings)
                .Select(m => m.SourceField)
                .Distinct());

            if (existing != null)
            {
                var update = new Entity("sdkmessageprocessingstepimage", existing.Id)
                {
                    ["attributes"] = attributes
                };

                _service.Update(update);
                AddToSolution(solutionUniqueName, existing.Id, 93);
                return;
            }

            var image = new Entity("sdkmessageprocessingstepimage")
            {
                ["name"] = "PreImage",
                ["entityalias"] = "PreImage",
                ["imagetype"] = new OptionSetValue(0),
                ["messagepropertyname"] = "Target",
                ["sdkmessageprocessingstepid"] = new EntityReference("sdkmessageprocessingstep", stepId),
                ["attributes"] = attributes
            };

            var id = _service.Create(image);
            AddToSolution(solutionUniqueName, id, 93);
        }

        private Guid GetMessageId(string messageName)
        {
            var query = new QueryExpression("sdkmessage")
            {
                ColumnSet = new ColumnSet("name"),
                Criteria = new FilterExpression(LogicalOperator.And)
            };

            query.Criteria.AddCondition("name", ConditionOperator.Equal, messageName);
            var result = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
            if (result == null)
            {
                throw new InvalidOperationException($"SDK message '{messageName}' not found.");
            }

            return result.Id;
        }

        private Guid GetMessageFilterId(Guid messageId, string primaryObjectTypeCode)
        {
            var query = new QueryExpression("sdkmessagefilter")
            {
                ColumnSet = new ColumnSet(true),
                Criteria = new FilterExpression(LogicalOperator.And)
            };

            query.Criteria.AddCondition("sdkmessageid", ConditionOperator.Equal, messageId);
            query.Criteria.AddCondition("primaryobjecttypecode", ConditionOperator.Equal, primaryObjectTypeCode);
            var result = _service.RetrieveMultiple(query).Entities.FirstOrDefault();
            if (result == null)
            {
                throw new InvalidOperationException($"SDK message filter for '{primaryObjectTypeCode}' not found.");
            }

            return result.Id;
        }

        private void AddToSolution(string solutionUniqueName, Guid componentId, int componentType)
        {
            var request = new AddSolutionComponentRequest
            {
                ComponentId = componentId,
                ComponentType = componentType,
                SolutionUniqueName = solutionUniqueName,
                AddRequiredComponents = false
            };

            try
            {
                _service.Execute(request);
            }
            catch (System.ServiceModel.FaultException<Microsoft.Xrm.Sdk.OrganizationServiceFault> ex)
            {
                // Ignore duplicates when the component is already in the solution
                if (ex.Detail != null && ex.Detail.ErrorCode == -2147220685)
                {
                    return;
                }

                throw;
            }
        }
    }
}
