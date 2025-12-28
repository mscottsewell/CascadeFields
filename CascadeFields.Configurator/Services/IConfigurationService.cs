using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CascadeFields.Configurator.Models.Domain;
using CascadeFields.Configurator.Models.UI;

namespace CascadeFields.Configurator.Services
{
    /// <summary>
    /// Interface for configuration operations (publishing, retrieving)
    /// </summary>
    public interface IConfigurationService
    {
        /// <summary>
        /// Gets all existing configurations from plugin steps
        /// </summary>
        Task<List<ConfiguredRelationship>> GetExistingConfigurationsAsync();

        /// <summary>
        /// Publishes a configuration to a plugin step
        /// </summary>
        /// <param name="configuration">Configuration to publish</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <param name="solutionId">Optional solution ID to add the step to</param>
        Task PublishConfigurationAsync(
            CascadeConfigurationModel configuration,
            IProgress<string> progress,
            CancellationToken cancellationToken,
            Guid? solutionId = null);

        /// <summary>
        /// Gets existing configuration for a specific parent entity
        /// </summary>
        /// <param name="parentEntityLogicalName">Logical name of the parent entity</param>
        Task<string?> GetConfigurationForParentEntityAsync(string parentEntityLogicalName);
    }
}
