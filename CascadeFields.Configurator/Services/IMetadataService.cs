using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CascadeFields.Configurator.Models.UI;
using Microsoft.Xrm.Sdk.Metadata;
using CascadeFields.Configurator.Models;

namespace CascadeFields.Configurator.Services
{
    /// <summary>
    /// Defines operations for retrieving Dataverse metadata used by the configurator UI.
    /// </summary>
    /// <remarks>
    /// <para><b>Purpose:</b></para>
    /// This interface provides a clean abstraction over Dataverse metadata queries, making it easier
    /// to test the configurator UI and mock metadata operations. It focuses on the specific metadata
    /// queries needed for cascade configuration (solutions, entities, attributes, relationships).
    ///
    /// <para><b>Caching Strategy:</b></para>
    /// Implementations should cache metadata where appropriate (solution entities, relationships) to
    /// minimize round-trips to Dataverse and improve UI responsiveness.
    ///
    /// <para><b>Progress Reporting:</b></para>
    /// Methods that may take significant time (like loading solution entities) accept an optional
    /// IProgress parameter to report status back to the UI.
    /// </remarks>
    public interface IMetadataService
    {
        /// <summary>
        /// Retrieves all unmanaged, visible solutions from the Dataverse environment.
        /// </summary>
        /// <returns>A list of solution items ordered alphabetically by friendly name, with the Default solution at the top.</returns>
        /// <remarks>
        /// Filters out system solutions (Active, Basic, Common) and the "Common Data Service Default Solution".
        /// Only returns solutions that can have components added to them (unmanaged and visible).
        /// </remarks>
        Task<List<SolutionItem>> GetUnmanagedSolutionsAsync();

        /// <summary>
        /// Retrieves full metadata for all entities that are components of a specific solution.
        /// </summary>
        /// <param name="solutionUniqueName">The unique name of the solution (e.g., "Default", "CascadeFields").</param>
        /// <param name="progress">Optional progress reporter for tracking metadata load status.</param>
        /// <returns>A list of entity metadata objects with attributes and relationships.</returns>
        /// <exception cref="ArgumentNullException">Thrown when solutionUniqueName is null or whitespace.</exception>
        /// <remarks>
        /// <para><b>Performance:</b></para>
        /// This method uses RetrieveMetadataChangesRequest with filters to only retrieve entities in the
        /// specified solution. This is significantly faster than retrieving all entities and filtering in memory.
        ///
        /// <para><b>Caching:</b></para>
        /// Results are cached per solution to avoid repeated expensive metadata queries.
        ///
        /// <para><b>Progress Reporting:</b></para>
        /// Reports progress as (current, total, message) through the IProgress interface.
        /// </remarks>
        Task<List<EntityMetadata>> GetSolutionEntitiesAsync(string solutionUniqueName, IProgress<MetadataLoadProgress>? progress = null);

        /// <summary>
        /// Gets the count of entity components in a solution without loading full metadata.
        /// </summary>
        /// <param name="solutionUniqueName">The unique name of the solution.</param>
        /// <returns>The number of entity components in the solution.</returns>
        /// <exception cref="ArgumentNullException">Thrown when solutionUniqueName is null or whitespace.</exception>
        /// <remarks>
        /// This is a lightweight alternative to GetSolutionEntitiesAsync when you only need the count.
        /// Used for progress estimation and UI hints before loading full metadata.
        /// </remarks>
        Task<int> GetSolutionEntityCountAsync(string solutionUniqueName);

        /// <summary>
        /// Retrieves all attributes (fields) for a specific entity as UI-friendly attribute items.
        /// </summary>
        /// <param name="entityLogicalName">The logical name of the entity (e.g., "account", "contact").</param>
        /// <param name="includeReadOnly">If true, includes read-only attributes. Default is false.</param>
        /// <param name="includeLogical">If true, includes logical attributes (calculated, rollup). Default is false.</param>
        /// <returns>A list of attribute items sorted by display name.</returns>
        /// <remarks>
        /// <para><b>Filtering:</b></para>
        /// By default, only returns attributes that are valid for update (writable).
        /// Use includeReadOnly and includeLogical to expand the result set.
        ///
        /// <para><b>Usage:</b></para>
        /// Used to populate field selection dropdowns in the field mapping and filter criteria grids.
        /// </remarks>
        Task<List<AttributeItem>> GetAttributesAsync(string entityLogicalName, bool includeReadOnly = false, bool includeLogical = false);

        /// <summary>
        /// Retrieves all one-to-many relationships where the specified entity is the parent.
        /// </summary>
        /// <param name="parentEntityLogicalName">The logical name of the parent entity.</param>
        /// <param name="solutionUniqueName">Optional solution unique name to filter child entities to those in the solution.</param>
        /// <returns>A list of relationship items representing potential child relationships for cascade configuration.</returns>
        /// <remarks>
        /// <para><b>Solution Filtering:</b></para>
        /// If solutionUniqueName is provided, only relationships where the child entity exists in that
        /// solution are returned. If null or "Active", uses all solution entities.
        ///
        /// <para><b>Caching:</b></para>
        /// Results are cached per parent entity and solution combination.
        ///
        /// <para><b>Usage:</b></para>
        /// Used to populate the relationship picker dialog when adding child relationships to a cascade configuration.
        /// </remarks>
        Task<List<RelationshipItem>> GetChildRelationshipsAsync(string parentEntityLogicalName, string? solutionUniqueName = null);

        /// <summary>
        /// Retrieves complete entity metadata including all attributes and relationships.
        /// </summary>
        /// <param name="entityLogicalName">The logical name of the entity.</param>
        /// <returns>The complete entity metadata object.</returns>
        /// <exception cref="ArgumentNullException">Thrown when entityLogicalName is null or whitespace.</exception>
        /// <remarks>
        /// <para><b>Filters:</b></para>
        /// Requests Entity, Attributes, and Relationships filters with RetrieveAsIfPublished=true
        /// to ensure all metadata is returned, including unpublished customizations.
        ///
        /// <para><b>Performance:</b></para>
        /// This is a relatively expensive operation. Use GetSolutionEntitiesAsync for bulk entity retrieval
        /// or cache results when calling multiple times for the same entity.
        /// </remarks>
        Task<EntityMetadata> GetEntityMetadataAsync(string entityLogicalName);
    }
}
