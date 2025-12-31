using CascadeFields.Plugin.Helpers;
using CascadeFields.Plugin.Models;
using CascadeFields.Plugin.Services;
using Microsoft.Xrm.Sdk;
using System;

namespace CascadeFields.Plugin
{
    /// <summary>
    /// Main plugin implementation that automatically cascades field values from parent records to related child records in Microsoft Dataverse.
    /// </summary>
    /// <remarks>
    /// <para><b>Execution Modes:</b></para>
    /// <list type="bullet">
    ///     <item>
    ///         <term>Parent Mode</term>
    ///         <description>Triggered when the parent entity is updated. Cascades configured field values to all related child records.
    ///         Register on <b>Update</b> message, <b>Post-operation</b> stage (Stage 40), <b>Asynchronous</b> mode recommended for performance.</description>
    ///     </item>
    ///     <item>
    ///         <term>Child Mode</term>
    ///         <description>Triggered when a child record is created or the lookup field is changed. Copies field values from the parent at the moment of association.
    ///         Register on <b>Create</b> and <b>Update</b> messages, <b>Pre-operation</b> stage (Stage 20), <b>Synchronous</b> mode required.
    ///         Add filtering attributes to include only the lookup field that references the parent entity.</description>
    ///     </item>
    /// </list>
    /// <para><b>Configuration:</b></para>
    /// Configuration is provided as JSON in the plugin's Unsecure Configuration during step registration.
    /// The configuration defines the parent entity, related child entities, field mappings, and optional filters.
    /// Use the CascadeFields Configurator tool (XrmToolBox plugin) to generate and publish configurations.
    ///
    /// <para><b>Recursion Prevention:</b></para>
    /// The plugin implements depth checking (max depth: 2) to prevent infinite loops if cascades trigger additional updates.
    /// If depth exceeds 2, execution is automatically terminated with a warning.
    ///
    /// <para><b>Error Handling:</b></para>
    /// All errors are wrapped in InvalidPluginExecutionException with descriptive messages.
    /// Detailed error information is logged to the plugin trace log.
    /// </remarks>
    /// <example>
    /// Example registration for parent-side cascade (Account to Contact):
    /// <code>
    /// Message: Update
    /// Primary Entity: account
    /// Stage: Post-operation (40)
    /// Execution Mode: Asynchronous
    /// Unsecure Configuration: [JSON configuration with field mappings]
    /// </code>
    /// </example>
    public class CascadeFieldsPlugin : IPlugin
    {
        /// <summary>
        /// Stores the JSON configuration that defines cascade behavior, loaded from plugin step registration.
        /// </summary>
        private readonly string _unsecureConfiguration;

        /// <summary>
        /// Initializes a new instance of the CascadeFieldsPlugin with configuration settings.
        /// This constructor is called automatically by the Dataverse platform when the plugin step executes.
        /// </summary>
        /// <param name="unsecureConfiguration">
        /// JSON string containing the cascade configuration (parent entity, child entities, field mappings, filters).
        /// This configuration is stored in the plugin step's Unsecure Configuration field and is visible to all users.
        /// Generate this JSON using the CascadeFields Configurator tool.
        /// </param>
        /// <param name="secureConfiguration">
        /// Secure configuration string (currently unused). Reserved for future use with sensitive data that should only be visible to administrators.
        /// </param>
        public CascadeFieldsPlugin(string unsecureConfiguration, string secureConfiguration)
        {
            _unsecureConfiguration = unsecureConfiguration;
        }

        /// <summary>
        /// Main plugin execution method called by the Dataverse platform when a configured message and entity trigger the plugin.
        /// </summary>
        /// <param name="serviceProvider">
        /// Service provider that provides access to platform services including:
        /// - ITracingService for diagnostic logging
        /// - IPluginExecutionContext for execution context information (message, entity, stage, depth, etc.)
        /// - IOrganizationServiceFactory for creating service instances to interact with Dataverse
        /// </param>
        /// <exception cref="InvalidPluginExecutionException">
        /// Thrown when configuration is invalid, cascade operations fail, or unexpected errors occur.
        /// Exception messages are logged to the plugin trace log and surfaced to the user.
        /// </exception>
        /// <remarks>
        /// <para><b>Execution Flow:</b></para>
        /// <list type="number">
        ///     <item>Initialize services and logging (IOrganizationService, PluginTracer)</item>
        ///     <item>Check execution depth to prevent infinite recursion (max: 2)</item>
        ///     <item>Load and validate JSON configuration from unsecure configuration</item>
        ///     <item>Verify configuration applies to the current entity (parent or child)</item>
        ///     <item>Retrieve target entity and pre-image from execution context</item>
        ///     <item>Determine execution mode (Parent Update or Child Create/Update)</item>
        ///     <item>Execute appropriate cascade logic via CascadeService</item>
        /// </list>
        /// </remarks>
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

                // Prevent runaway recursion
                if (context.Depth > 2)
                {
                    tracer.Warning($"Execution depth {context.Depth} exceeds maximum (2). Stopping to prevent infinite loop.");
                    return;
                }

                // Load configuration
                var configManager = new ConfigurationManager(service, tracer);
                var config = configManager.LoadConfiguration(_unsecureConfiguration);

                // Apply tracing configuration
                tracer.SetTracingEnabled(config.EnableTracing);
                tracer.Debug($"Tracing enabled: {config.EnableTracing}");

                // Check if configuration applies to this entity (parent or child)
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

                // Determine execution mode based on entity + message
                if (context.PrimaryEntityName.Equals(config.ParentEntity, StringComparison.OrdinalIgnoreCase)
                    && context.MessageName.Equals("Update", StringComparison.OrdinalIgnoreCase))
                {
                    // Parent-side cascade: ensure post-op async registration recommended
                    if (context.Stage != 40)
                    {
                        tracer.Warning($"Parent update detected on unexpected stage {context.Stage}. Recommended: 40 (Post-operation)");
                    }

                    // Perform cascade operation to children
                    tracer.Info("Beginning cascade operation to related children");
                    cascadeService.CascadeFieldValues(target, preImage, config);
                }
                else if (IsChildEntity(config, context.PrimaryEntityName) &&
                         (context.MessageName.Equals("Create", StringComparison.OrdinalIgnoreCase) ||
                          context.MessageName.Equals("Update", StringComparison.OrdinalIgnoreCase)))
                {
                    // Child-side handling: copy mapped values from parent → child when created or relinked
                    if (context.Stage != 20)
                    {
                        tracer.Warning($"Child handling on stage {context.Stage}. Recommended: 20 (Pre-operation)");
                    }

                    cascadeService.ApplyParentValuesToChildOnAttachOrCreate(context, target, preImage, config);
                }
                else
                {
                    tracer.Info("No applicable execution mode for this context.");
                }

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
        /// Determines if the specified entity is configured as a child entity in the cascade configuration.
        /// Used to identify when the plugin is executing in "child mode" vs "parent mode".
        /// </summary>
        /// <param name="config">The loaded cascade configuration containing all related entity definitions.</param>
        /// <param name="entityName">The logical name of the entity to check (e.g., "contact", "opportunity").</param>
        /// <returns>
        /// <c>true</c> if the entity is defined as a related/child entity in the configuration; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This method performs a case-insensitive comparison against all entity names in the RelatedEntities collection.
        /// If the configuration is null or has no related entities, this method returns false.
        /// </remarks>
        private bool IsChildEntity(CascadeConfiguration config, string entityName)
        {
            if (config?.RelatedEntities == null) return false;
            foreach (var r in config.RelatedEntities)
            {
                if (r.EntityName.Equals(entityName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Retrieves the target entity from the plugin execution context's input parameters.
        /// The target entity represents the record being created, updated, or operated on.
        /// </summary>
        /// <param name="context">The plugin execution context containing input parameters.</param>
        /// <param name="tracer">The tracer instance for logging diagnostic information.</param>
        /// <returns>
        /// The target <see cref="Entity"/> if present in InputParameters; otherwise, <c>null</c>.
        /// For Create operations, the target contains only the fields being set.
        /// For Update operations, the target contains only the fields being changed.
        /// </returns>
        /// <remarks>
        /// The target entity is available in the "Target" key of InputParameters for Create, Update, and Delete messages.
        /// For Update operations, combine with the PreImage to get the complete record state (changed + unchanged fields).
        /// </remarks>
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
        /// Retrieves the pre-image entity from the plugin execution context.
        /// The pre-image represents the state of the record before the current operation.
        /// </summary>
        /// <param name="context">The plugin execution context containing pre-entity images.</param>
        /// <param name="tracer">The tracer instance for logging diagnostic information.</param>
        /// <returns>
        /// The pre-image <see cref="Entity"/> if configured and present in PreEntityImages with the key "PreImage"; otherwise, <c>null</c>.
        /// </returns>
        /// <remarks>
        /// <para><b>Pre-Image Configuration:</b></para>
        /// Pre-images must be explicitly configured when registering the plugin step.
        /// The image alias must be set to "PreImage" (case-sensitive) for this method to retrieve it.
        /// Include all fields that are used in trigger field logic or that need to be compared against changed values.
        ///
        /// <para><b>Usage:</b></para>
        /// Pre-images are primarily used in Update operations to access the complete record state before changes.
        /// This allows the plugin to determine which fields changed and what their previous values were.
        /// For Create operations, pre-images are not available (returns null).
        /// </remarks>
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
