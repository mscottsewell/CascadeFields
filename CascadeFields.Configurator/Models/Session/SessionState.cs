using System;

namespace CascadeFields.Configurator.Models.Session
{
    /// <summary>
    /// Represents persisted session state for a connection
    /// Saved to disk between app sessions
    /// </summary>
    public class SessionState
    {
        /// <summary>
        /// Unique identifier for this connection
        /// </summary>
        public string ConnectionId { get; set; } = string.Empty;

        /// <summary>
        /// Selected solution unique name
        /// </summary>
        public string? SolutionUniqueName { get; set; }

        /// <summary>
        /// Selected parent entity logical name
        /// </summary>
        public string? ParentEntityLogicalName { get; set; }

        /// <summary>
        /// JSON representation of the current configuration
        /// </summary>
        public string? ConfigurationJson { get; set; }

        /// <summary>
        /// When this session was last modified
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Checks if this session has enough data to restore
        /// </summary>
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(SolutionUniqueName) &&
            !string.IsNullOrWhiteSpace(ParentEntityLogicalName);
    }
}
