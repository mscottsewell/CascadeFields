using CascadeFields.Plugin.Models;
using Microsoft.Xrm.Sdk;
using Newtonsoft.Json;
using System;
using InvalidPluginExecutionException = Microsoft.Xrm.Sdk.InvalidPluginExecutionException;

namespace CascadeFields.Plugin.Helpers
{
    /// <summary>
    /// Manages loading, parsing, and validation of cascade field configurations from JSON.
    /// Provides helper methods to determine if a configuration applies to a given execution context.
    /// </summary>
    /// <remarks>
    /// This class is responsible for deserializing the JSON configuration string stored in the plugin step's
    /// Unsecure Configuration field and converting it into a strongly-typed <see cref="CascadeConfiguration"/> object.
    /// It also validates the configuration and logs detailed information about the loaded configuration.
    /// </remarks>
    public class ConfigurationManager
    {
        /// <summary>
        /// Dataverse organization service (currently unused, reserved for future functionality like loading configurations from database).
        /// </summary>
        private readonly IOrganizationService _service;

        /// <summary>
        /// Plugin tracer for logging configuration loading progress and validation results.
        /// </summary>
        private readonly PluginTracer _tracer;

        /// <summary>
        /// Initializes a new instance of the <see cref="ConfigurationManager"/> class.
        /// </summary>
        /// <param name="service">The Dataverse organization service. Cannot be null.</param>
        /// <param name="tracer">The plugin tracer for diagnostic logging. Cannot be null.</param>
        /// <exception cref="ArgumentNullException">Thrown if service or tracer is null.</exception>
        public ConfigurationManager(IOrganizationService service, PluginTracer tracer)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _tracer = tracer ?? throw new ArgumentNullException(nameof(tracer));
        }

        /// <summary>
        /// Loads and parses the cascade configuration from a JSON string, validates it, and logs configuration details.
        /// </summary>
        /// <param name="configurationJson">
        /// JSON string containing the cascade configuration. This should be the value from the plugin step's Unsecure Configuration field.
        /// </param>
        /// <returns>
        /// A validated <see cref="CascadeConfiguration"/> object ready for use by the cascade service.
        /// </returns>
        /// <exception cref="InvalidPluginExecutionException">
        /// Thrown when:
        /// <list type="bullet">
        ///     <item><description>Configuration JSON is null, empty, or whitespace</description></item>
        ///     <item><description>JSON deserialization fails (invalid JSON syntax)</description></item>
        ///     <item><description>Deserialized configuration is null</description></item>
        ///     <item><description>Configuration validation fails (missing required fields, invalid relationships, etc.)</description></item>
        /// </list>
        /// </exception>
        /// <remarks>
        /// <para><b>Logging:</b></para>
        /// Logs configuration summary including name, ID, parent entity, number of related entities, and field mapping counts.
        /// This information appears in the plugin trace log and is helpful for troubleshooting.
        ///
        /// <para><b>Validation:</b></para>
        /// Calls <see cref="CascadeConfiguration.Validate"/> to ensure all required fields are present and properly configured.
        /// Validation errors are thrown as InvalidPluginExecutionException with descriptive messages.
        /// </remarks>
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
        /// Determines whether a cascade configuration applies to the current plugin execution context based on the entity being processed.
        /// </summary>
        /// <param name="config">The loaded cascade configuration to evaluate.</param>
        /// <param name="entityName">The logical name of the entity triggering the plugin (from context.PrimaryEntityName).</param>
        /// <returns>
        /// <c>true</c> if the configuration should be processed for this entity; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// <para><b>Applicability Rules:</b></para>
        /// A configuration is considered applicable when ALL of the following are true:
        /// <list type="number">
        ///     <item><description>The configuration's <see cref="CascadeConfiguration.IsActive"/> property is true</description></item>
        ///     <item><description>The entity name matches either:
        ///         <list type="bullet">
        ///             <item><description>The configured parent entity (for parent-side cascades)</description></item>
        ///             <item><description>Any configured related/child entity (for child-side operations)</description></item>
        ///         </list>
        ///     </description></item>
        /// </list>
        ///
        /// <para><b>Usage:</b></para>
        /// This method is called early in plugin execution to short-circuit processing when the configuration
        /// doesn't apply to the current entity. This prevents unnecessary work and improves performance.
        ///
        /// <para><b>Inactive Configurations:</b></para>
        /// If IsActive is false, the plugin skips processing entirely. This allows temporarily disabling
        /// a configuration without deleting the plugin step.
        /// </remarks>
        public bool IsConfigurationApplicable(CascadeConfiguration config, string entityName)
        {
            if (!config.IsActive)
            {
                _tracer.Info("Configuration is not active, skipping execution");
                return false;
            }

            // Apply when running on the configured parent entity or any configured child entity
            if (config.ParentEntity.Equals(entityName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (config.RelatedEntities != null &&
                config.RelatedEntities.Exists(r => r.EntityName.Equals(entityName, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            _tracer.Warning($"Configuration does not apply to entity '{entityName}'. Parent: '{config.ParentEntity}'.");
            return false;
        }
    }
}
