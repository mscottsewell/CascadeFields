using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CascadeFields.Configurator.Models.Domain;
using CascadeFields.Configurator.Models.UI;

namespace CascadeFields.Configurator.Services
{
    /// <summary>
    /// Defines operations for managing CascadeFields plugin configurations in Dataverse.
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b></para>
    /// This interface provides operations for:
    /// <list type="bullet">
    ///     <item><description>Publishing cascade configurations as plugin steps</description></item>
    ///     <item><description>Retrieving existing configurations from Dataverse</description></item>
    ///     <item><description>Updating the CascadeFields plugin assembly</description></item>
    ///     <item><description>Managing plugin components and solution membership</description></item>
    /// </list>
    ///
    /// <para><b>Architecture:</b></para>
    /// Configurations are stored as JSON in the configuration field of SDK message processing steps.
    /// The service manages the full lifecycle: plugin assembly registration, plugin type registration,
    /// step creation/update, pre-image configuration, and solution component membership.
    ///
    /// <para><b>Progress Reporting:</b></para>
    /// Methods that perform multiple operations accept an IProgress parameter to report status
    /// updates back to the UI, keeping users informed during long-running operations.
    /// </remarks>
    public interface IConfigurationService
    {
        /// <summary>
        /// Retrieves all existing CascadeFields configurations by querying plugin steps.
        /// </summary>
        /// <returns>A list of configured relationships with parsed configuration data and metadata.</returns>
        /// <remarks>
        /// <para><b>Query Strategy:</b></para>
        /// Queries sdkmessageprocessingstep records where:
        /// <list type="bullet">
        ///     <item><description>plugintype references CascadeFieldsPlugin</description></item>
        ///     <item><description>configuration field is not null</description></item>
        /// </list>
        ///
        /// <para><b>Metadata Resolution:</b></para>
        /// For each configuration, resolves friendly display names for child entities and lookup fields.
        /// Falls back to logical names if metadata retrieval fails.
        ///
        /// <para><b>Deduplication:</b></para>
        /// Groups by parent entity, child entity, lookup field, and relationship name to avoid
        /// duplicate entries when multiple steps share the same configuration.
        /// </remarks>
        Task<List<ConfiguredRelationship>> GetExistingConfigurationsAsync();

        /// <summary>
        /// Publishes a cascade configuration to Dataverse by creating or updating plugin steps.
        /// </summary>
        /// <param name="configuration">The cascade configuration to publish.</param>
        /// <param name="progress">Progress reporter for status updates.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <param name="solutionId">Optional solution ID to add plugin components to.</param>
        /// <exception cref="ArgumentNullException">Thrown when configuration is null.</exception>
        /// <exception cref="InvalidPluginExecutionException">Thrown when plugin type is not found.</exception>
        /// <remarks>
        /// <para><b>Steps Created:</b></para>
        /// For each configuration, creates or updates:
        /// <list type="number">
        ///     <item><description><b>Parent Update Step:</b> Post-operation async step on parent entity Update message</description></item>
        ///     <item><description><b>Parent PreImage:</b> PreImage containing all source fields</description></item>
        ///     <item><description><b>Child Create Steps:</b> Pre-operation sync step on child entity Create message (for each child relationship)</description></item>
        ///     <item><description><b>Child Update Steps:</b> Pre-operation sync step on child entity Update message (for relink scenarios)</description></item>
        ///     <item><description><b>Child PreImages:</b> PreImage containing lookup fields for relink detection</description></item>
        /// </list>
        ///
        /// <para><b>Filtering Attributes:</b></para>
        /// Parent step includes only trigger fields in its filtering attributes to optimize performance.
        /// Child update steps include lookup fields to trigger only on relationship changes.
        ///
        /// <para><b>Solution Membership:</b></para>
        /// If solutionId is provided, adds all plugin components (assembly, type, steps, images) to that solution.
        /// </remarks>
        Task PublishConfigurationAsync(
            CascadeConfigurationModel configuration,
            IProgress<string> progress,
            CancellationToken cancellationToken,
            Guid? solutionId = null);

        /// <summary>
        /// Retrieves the raw JSON configuration for a specific parent entity.
        /// </summary>
        /// <param name="parentEntityLogicalName">The logical name of the parent entity.</param>
        /// <returns>The JSON configuration string if found; otherwise, null.</returns>
        /// <remarks>
        /// This is a convenience method that queries existing configurations and returns the
        /// raw JSON for the first matching parent entity. Used when loading configurations for editing.
        /// </remarks>
        Task<string?> GetConfigurationForParentEntityAsync(string parentEntityLogicalName);

        /// <summary>
        /// Updates or registers the CascadeFields plugin assembly and plugin type in Dataverse.
        /// </summary>
        /// <param name="assemblyPath">The file path to CascadeFields.Plugin.dll.</param>
        /// <param name="progress">Progress reporter for status updates.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <param name="solutionId">Optional solution ID to add the assembly to.</param>
        /// <exception cref="InvalidPluginExecutionException">Thrown when assembly file is not found.</exception>
        /// <remarks>
        /// <para><b>Upsert Logic:</b></para>
        /// <list type="bullet">
        ///     <item><description>Reads assembly bytes from file and converts to base64</description></item>
        ///     <item><description>Creates or updates pluginassembly record</description></item>
        ///     <item><description>Creates or updates plugintype record for CascadeFieldsPlugin</description></item>
        ///     <item><description>Optionally adds assembly to specified solution</description></item>
        /// </list>
        ///
        /// <para><b>Assembly Settings:</b></para>
        /// <list type="bullet">
        ///     <item><description>IsolationMode: Sandbox (2)</description></item>
        ///     <item><description>SourceType: Database (0)</description></item>
        /// </list>
        ///
        /// <para><b>Usage:</b></para>
        /// Called when deploying or updating the plugin. Safe to call multiple times (idempotent).
        /// </remarks>
        Task UpdatePluginAssemblyAsync(
            string assemblyPath,
            IProgress<string> progress,
            CancellationToken cancellationToken,
            Guid? solutionId = null);

        /// <summary>
        /// Checks the registration status and version of the CascadeFields plugin in Dataverse.
        /// </summary>
        /// <param name="assemblyPath">The file path to CascadeFields.Plugin.dll for version comparison.</param>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        ///     <item><description>isRegistered: true if plugin assembly is registered</description></item>
        ///     <item><description>needsUpdate: true if local version is different from registered version</description></item>
        ///     <item><description>registeredVersion: assembly version registered in Dataverse</description></item>
        ///     <item><description>assemblyVersion: assembly version from local file</description></item>
        ///     <item><description>fileVersion: file version from local file</description></item>
        ///     <item><description>registeredFileVersion: file version from registered assembly</description></item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// <para><b>Version Comparison:</b></para>
        /// Compares both assembly version and file version. If either differs, needsUpdate is true.
        /// This ensures plugin updates are detected even if only the file version changes.
        ///
        /// <para><b>Error Handling:</b></para>
        /// Returns (false, false, null, null, null, null) if any error occurs during version checking.
        /// </remarks>
        (bool isRegistered, bool needsUpdate, string? registeredVersion, string? assemblyVersion, string? fileVersion, string? registeredFileVersion) CheckPluginStatus(string assemblyPath);

        /// <summary>
        /// Adds specified components to a solution, checking for existence to avoid duplicates.
        /// </summary>
        /// <param name="solutionId">The ID of the solution to add components to.</param>
        /// <param name="components">
        /// Collection of tuples containing:
        /// <list type="bullet">
        ///     <item><description>componentType: The component type code (1=Entity, 2=Attribute, 10=Relationship, etc.)</description></item>
        ///     <item><description>componentId: The GUID of the component</description></item>
        ///     <item><description>description: Human-readable description for progress reporting</description></item>
        /// </list>
        /// </param>
        /// <param name="progress">Progress reporter for status updates.</param>
        /// <remarks>
        /// <para><b>Deduplication:</b></para>
        /// Checks if each component already exists in the solution before attempting to add it.
        /// Reports "Already in solution" for components that exist, "Adding to solution" for new components.
        ///
        /// <para><b>Usage:</b></para>
        /// Used to ensure entities, attributes, and relationships referenced in cascade configurations
        /// are included in the target solution.
        /// </remarks>
        Task AddComponentsToSolutionAsync(Guid solutionId, IEnumerable<(int componentType, Guid componentId, string description)> components, IProgress<string> progress);

        /// <summary>
        /// Deletes plugin steps for relationships that have been removed from a configuration.
        /// </summary>
        /// <param name="parentEntityLogicalName">The logical name of the parent entity.</param>
        /// <param name="relationships">The collection of relationships to delete steps for.</param>
        /// <param name="progress">Progress reporter for status updates.</param>
        /// <remarks>
        /// <para><b>Steps Deleted:</b></para>
        /// For each relationship, deletes:
        /// <list type="bullet">
        ///     <item><description>Child Create step (named "CascadeFields (Child Create): {entityName}")</description></item>
        ///     <item><description>Child Update step (named "CascadeFields (Child Relink): {entityName}")</description></item>
        /// </list>
        ///
        /// <para><b>Error Handling:</b></para>
        /// If plugin type is not found, reports warning and returns without error.
        /// If individual steps don't exist, reports "No plugin step found" without error.
        ///
        /// <para><b>Usage:</b></para>
        /// Called when a user removes a child relationship from a configuration. Ensures orphaned
        /// plugin steps are cleaned up to avoid confusion and unnecessary plugin executions.
        /// </remarks>
        Task DeleteRelationshipStepsAsync(string parentEntityLogicalName, IEnumerable<RelatedEntityConfigModel> relationships, IProgress<string> progress);
    }
}
