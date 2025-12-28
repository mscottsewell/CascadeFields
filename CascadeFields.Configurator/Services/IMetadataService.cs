using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CascadeFields.Configurator.Models.UI;
using Microsoft.Xrm.Sdk.Metadata;

namespace CascadeFields.Configurator.Services
{
    /// <summary>
    /// Interface for metadata operations against Dataverse
    /// </summary>
    public interface IMetadataService
    {
        /// <summary>
        /// Gets all unmanaged solutions in the environment
        /// </summary>
        Task<List<SolutionItem>> GetUnmanagedSolutionsAsync();

        /// <summary>
        /// Gets all entities in a specific solution
        /// </summary>
        /// <param name="solutionUniqueName">Unique name of the solution</param>
        Task<List<EntityMetadata>> GetSolutionEntitiesAsync(string solutionUniqueName);

        /// <summary>
        /// Gets all attributes for a specific entity
        /// </summary>
        /// <param name="entityLogicalName">Logical name of the entity</param>
        Task<List<AttributeItem>> GetAttributesAsync(string entityLogicalName);

        /// <summary>
        /// Gets all child relationships for a parent entity
        /// </summary>
        /// <param name="parentEntityLogicalName">Logical name of the parent entity</param>
        Task<List<RelationshipItem>> GetChildRelationshipsAsync(string parentEntityLogicalName);

        /// <summary>
        /// Gets entity metadata by logical name
        /// </summary>
        /// <param name="entityLogicalName">Logical name of the entity</param>
        Task<EntityMetadata> GetEntityMetadataAsync(string entityLogicalName);
    }
}
