using System.Collections.Generic;
using System.Threading.Tasks;
using CascadeFields.Configurator.Models.Session;

namespace CascadeFields.Configurator.Services
{
    /// <summary>
    /// Defines the contract for persisting and retrieving session state across application sessions.
    /// </summary>
    /// <remarks>
    /// <para><strong>Purpose:</strong></para>
    /// <para>
    /// This interface abstracts session persistence operations, allowing the configurator to save
    /// user selections (entity, solution, configuration) and restore them when reconnecting to
    /// the same Dataverse environment.
    /// </para>
    ///
    /// <para><strong>Session Scope:</strong></para>
    /// <list type="bullet">
    /// <item><description>Sessions are per-connection (one per Dataverse environment)</description></item>
    /// <item><description>Each connection is identified by a unique connection ID</description></item>
    /// <item><description>Sessions persist between application restarts</description></item>
    /// <item><description>Sessions are independent - clearing one doesn't affect others</description></item>
    /// </list>
    ///
    /// <para><strong>Implementation Notes:</strong></para>
    /// <list type="bullet">
    /// <item><description>All operations are async to support potential I/O operations</description></item>
    /// <item><description>Methods should be tolerant of missing or corrupt data</description></item>
    /// <item><description>Failures should not throw exceptions that break the application</description></item>
    /// <item><description>The repository should gracefully degrade when persistence is unavailable</description></item>
    /// </list>
    /// </remarks>
    public interface ISettingsRepository
    {
        /// <summary>
        /// Loads the saved session state for a specific Dataverse connection.
        /// </summary>
        /// <param name="connectionId">The unique identifier for the Dataverse connection.</param>
        /// <returns>
        /// A task that resolves to the session state if found, or null if no session exists
        /// or if the session data is invalid/corrupted.
        /// </returns>
        /// <remarks>
        /// This method should return null rather than throwing exceptions when:
        /// <list type="bullet">
        /// <item><description>No session file exists for the connection (first-time use)</description></item>
        /// <item><description>The session file is corrupted or cannot be deserialized</description></item>
        /// <item><description>The connection ID is null or whitespace</description></item>
        /// </list>
        /// </remarks>
        Task<SessionState?> LoadSessionAsync(string connectionId);

        /// <summary>
        /// Persists the current session state for a Dataverse connection.
        /// </summary>
        /// <param name="session">The session state to persist, containing connection ID and user selections.</param>
        /// <returns>A task representing the asynchronous save operation.</returns>
        /// <remarks>
        /// <para>
        /// This method saves a snapshot of the current session, including:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Selected entity</description></item>
        /// <item><description>Selected solution</description></item>
        /// <item><description>Selected configuration (if editing existing)</description></item>
        /// <item><description>Metadata cache status</description></item>
        /// <item><description>Timestamp of last activity</description></item>
        /// </list>
        /// <para>
        /// Save failures should be logged but not throw exceptions that would disrupt
        /// the user workflow.
        /// </para>
        /// </remarks>
        Task SaveSessionAsync(SessionState session);

        /// <summary>
        /// Removes the persisted session state for a specific connection.
        /// </summary>
        /// <param name="connectionId">The unique identifier for the connection whose session should be cleared.</param>
        /// <returns>A task representing the asynchronous clear operation.</returns>
        /// <remarks>
        /// This method is typically called when:
        /// <list type="bullet">
        /// <item><description>The user explicitly resets their session</description></item>
        /// <item><description>A session becomes invalid (e.g., entity or solution deleted)</description></item>
        /// <item><description>The application needs to force a fresh start</description></item>
        /// </list>
        /// If no session exists for the connection, this method should complete successfully without error.
        /// </remarks>
        Task ClearSessionAsync(string connectionId);

        /// <summary>
        /// Retrieves all persisted sessions across all connections.
        /// </summary>
        /// <returns>
        /// A task that resolves to a dictionary mapping connection IDs to their session states.
        /// Returns an empty dictionary if no sessions exist or if the storage location is inaccessible.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method is useful for:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Administrative tools that need to view or manage all sessions</description></item>
        /// <item><description>Cleanup operations that remove stale sessions</description></item>
        /// <item><description>Migration or export scenarios</description></item>
        /// </list>
        /// <para>
        /// Invalid or corrupted session files should be skipped rather than causing the entire
        /// operation to fail.
        /// </para>
        /// </remarks>
        Task<Dictionary<string, SessionState>> GetAllSessionsAsync();
    }
}
