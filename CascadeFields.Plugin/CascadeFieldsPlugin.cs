using CascadeFields.Plugin.Helpers;
using CascadeFields.Plugin.Models;
using CascadeFields.Plugin.Services;
using Microsoft.Xrm.Sdk;
using System;

namespace CascadeFields.Plugin
{
    /// <summary>
    /// Plugin that cascades field values from a parent record to related child records
    /// Registers on: Update message, Post-operation, Asynchronous
    /// </summary>
    public class CascadeFieldsPlugin : IPlugin
    {
        private readonly string _unsecureConfiguration;
        private readonly string _secureConfiguration;

        /// <summary>
        /// Constructor for plugin registration
        /// </summary>
        /// <param name="unsecureConfiguration">Configuration JSON containing cascade rules</param>
        /// <param name="secureConfiguration">Secure configuration (not used currently)</param>
        public CascadeFieldsPlugin(string unsecureConfiguration, string secureConfiguration)
        {
            _unsecureConfiguration = unsecureConfiguration;
            _secureConfiguration = secureConfiguration;
        }

        /// <summary>
        /// Main plugin execution method
        /// </summary>
        public void Execute(IServiceProvider serviceProvider)
        {
            // Obtain services
            ITracingService tracingService = (ITracingService)serviceProvider.GetService(typeof(ITracingService));
            IPluginExecutionContext context = (IPluginExecutionContext)serviceProvider.GetService(typeof(IPluginExecutionContext));
            IOrganizationServiceFactory serviceFactory = (IOrganizationServiceFactory)serviceProvider.GetService(typeof(IOrganizationServiceFactory));
            IOrganizationService service = serviceFactory.CreateOrganizationService(context.UserId);

            PluginTracer tracer = null;

            try
            {
                // Initialize tracer
                tracer = new PluginTracer(tracingService, "CascadeFieldsPlugin");
                tracer.Info("=== Plugin Execution Started ===");
                tracer.LogContextDetails(context);

                // Validate context
                if (!ValidateContext(context, tracer))
                {
                    tracer.Info("Context validation failed, skipping execution");
                    return;
                }

                // Load configuration
                var configManager = new ConfigurationManager(service, tracer);
                var config = configManager.LoadConfiguration(_unsecureConfiguration);

                // Apply tracing configuration
                tracer.SetTracingEnabled(config.EnableTracing);
                tracer.Debug($"Tracing enabled: {config.EnableTracing}");

                // Check if configuration applies to this entity
                if (!configManager.IsConfigurationApplicable(config, context.PrimaryEntityName))
                {
                    tracer.Info("Configuration not applicable to this entity");
                    return;
                }

                // Get target and pre-image
                Entity target = GetTarget(context, tracer);
                Entity preImage = GetPreImage(context, tracer);

                if (target == null)
                {
                    tracer.Warning("Target entity not found in context");
                    return;
                }

                // Initialize cascade service
                var cascadeService = new CascadeService(service, tracer);

                // Check if any trigger fields changed
                if (!cascadeService.HasTriggerFieldChanged(target, preImage, config))
                {
                    tracer.Info("No trigger fields changed, skipping cascade");
                    return;
                }

                // Perform cascade operation
                tracer.Info("Beginning cascade operation");
                cascadeService.CascadeFieldValues(target, preImage, config);

                tracer.Info("=== Plugin Execution Completed Successfully ===");
            }
            catch (InvalidPluginExecutionException ex)
            {
                tracer?.Error("Plugin execution failed with validation error", ex);
                throw new InvalidPluginExecutionException($"CascadeFields Plugin Error: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                tracer?.Error("Plugin execution failed with unexpected error", ex);
                throw new InvalidPluginExecutionException(
                    $"CascadeFields Plugin encountered an unexpected error: {ex.Message}. See trace log for details.", ex);
            }
        }

        /// <summary>
        /// Validates the execution context
        /// </summary>
        private bool ValidateContext(IPluginExecutionContext context, PluginTracer tracer)
        {
            if (context.MessageName != "Update")
            {
                tracer.Warning($"Plugin registered on incorrect message: {context.MessageName}. Expected: Update");
                return false;
            }

            if (context.Stage != 40) // Post-operation
            {
                tracer.Warning($"Plugin registered on incorrect stage: {context.Stage}. Expected: 40 (Post-operation)");
                return false;
            }

            if (context.Depth > 2)
            {
                tracer.Warning($"Execution depth {context.Depth} exceeds maximum (2). Stopping to prevent infinite loop.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Retrieves the target entity from context
        /// </summary>
        private Entity GetTarget(IPluginExecutionContext context, PluginTracer tracer)
        {
            if (context.InputParameters.Contains("Target") && context.InputParameters["Target"] is Entity target)
            {
                tracer.Debug($"Target entity retrieved: {target.LogicalName} ({target.Id})");
                return target;
            }

            return null;
        }

        /// <summary>
        /// Retrieves the pre-image from context
        /// </summary>
        private Entity GetPreImage(IPluginExecutionContext context, PluginTracer tracer)
        {
            if (context.PreEntityImages.Contains("PreImage"))
            {
                var preImage = context.PreEntityImages["PreImage"];
                tracer.Debug($"PreImage retrieved: {preImage.LogicalName} with {preImage.Attributes.Count} attributes");
                return preImage;
            }

            tracer.Debug("No PreImage found in context");
            return null;
        }
    }
}
