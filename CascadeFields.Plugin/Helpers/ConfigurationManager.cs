using CascadeFields.Plugin.Models;
using Microsoft.Xrm.Sdk;
using Newtonsoft.Json;
using System;
using InvalidPluginExecutionException = Microsoft.Xrm.Sdk.InvalidPluginExecutionException;

namespace CascadeFields.Plugin.Helpers
{
    /// <summary>
    /// Manages configuration loading and parsing
    /// </summary>
    public class ConfigurationManager
    {
        private readonly IOrganizationService _service;
        private readonly PluginTracer _tracer;

        public ConfigurationManager(IOrganizationService service, PluginTracer tracer)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
        }

        /// <summary>
        /// Loads configuration from unsecure configuration string
        /// </summary>
        public CascadeConfiguration LoadConfiguration(string configurationJson)
        {
            _tracer.StartOperation("LoadConfiguration");

            try
            {
                if (string.IsNullOrWhiteSpace(configurationJson))
                {
                    throw new InvalidPluginExecutionException(
                        "No configuration found. Please provide configuration in the plugin step's unsecure configuration.");
                }

                _tracer.Debug($"Parsing configuration JSON (length: {configurationJson.Length})");

                var config = JsonConvert.DeserializeObject<CascadeConfiguration>(configurationJson);

                if (config == null)
                {
                    throw new InvalidPluginExecutionException("Failed to deserialize cascade configuration.");
                }

                _tracer.Info($"Configuration loaded: {config.Name} (Id: {config.Id})");
                _tracer.Info($"Parent Entity: {config.ParentEntity}");
                _tracer.Info($"Related Entities: {config.RelatedEntities?.Count ?? 0}");
                
                // Log field mappings per entity
                if (config.RelatedEntities != null)
                {
                    foreach (var entity in config.RelatedEntities)
                    {
                        _tracer.Info($"  - {entity.EntityName}: {entity.FieldMappings?.Count ?? 0} field mappings");
                    }
                }

                // Validate configuration
                config.Validate();
                _tracer.Info("Configuration validation passed");

                _tracer.EndOperation("LoadConfiguration");
                return config;
            }
            catch (JsonException ex)
            {
                _tracer.Error("Failed to parse configuration JSON", ex);
                throw new InvalidPluginExecutionException(
                    $"Invalid configuration JSON: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                _tracer.Error("Error loading configuration", ex);
                throw;
            }
        }

        /// <summary>
        /// Validates that the configuration applies to the current context
        /// </summary>
        public bool IsConfigurationApplicable(CascadeConfiguration config, string entityName)
        {
            if (!config.IsActive)
            {
                _tracer.Info("Configuration is not active, skipping execution");
                return false;
            }

            if (!config.ParentEntity.Equals(entityName, StringComparison.OrdinalIgnoreCase))
            {
                _tracer.Warning($"Configuration parent entity '{config.ParentEntity}' does not match context entity '{entityName}'");
                return false;
            }

            return true;
        }
    }
}
