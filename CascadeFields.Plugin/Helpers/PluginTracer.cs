using Microsoft.Xrm.Sdk;
using System;
using System.Diagnostics;

namespace CascadeFields.Plugin.Helpers
{
    /// <summary>
    /// Provides comprehensive tracing, logging, and performance monitoring functionality for Dataverse plugins.
    /// Wraps the ITracingService with enhanced formatting, log levels, and execution timing capabilities.
    /// </summary>
    /// <remarks>
    /// <para><b>Features:</b></para>
    /// <list type="bullet">
    ///     <item><description><b>Log Levels:</b> Supports INFO, WARNING, ERROR, and DEBUG severity levels</description></item>
    ///     <item><description><b>Performance Timing:</b> Automatically tracks elapsed time since plugin started and for individual operations</description></item>
    ///     <item><description><b>Structured Format:</b> All logs include timestamp, level, plugin name, and elapsed milliseconds</description></item>
    ///     <item><description><b>Context Logging:</b> Dedicated method to log plugin execution context details (message, stage, entity, user, etc.)</description></item>
    ///     <item><description><b>Enable/Disable:</b> Tracing can be disabled at runtime for production environments to reduce log verbosity</description></item>
    ///     <item><description><b>Exception Formatting:</b> Error logs include exception type, message, and stack trace</description></item>
    /// </list>
    ///
    /// <para><b>Log Format:</b></para>
    /// <code>[YYYY-MM-DD HH:mm:ss.fff] [LEVEL] [PluginName] [+XXXms] Message</code>
    /// Example: [2025-01-11 14:23:45.123] [INFO] [CascadeFieldsPlugin] [+45ms] Starting operation: CascadeFieldValues
    ///
    /// <para><b>Performance Monitoring:</b></para>
    /// The built-in stopwatch starts when the tracer is created and tracks cumulative elapsed time.
    /// Use StartOperation/EndOperation pairs to measure specific operation durations.
    ///
    /// <para><b>Production Usage:</b></para>
    /// Set EnableTracing=false in the cascade configuration to disable verbose logging in production.
    /// Error logs are always written regardless of the enabled state (for critical error visibility).
    /// </remarks>
    public class PluginTracer
    {
        /// <summary>
        /// Dataverse tracing service that writes to the plugin trace log.
        /// </summary>
        private readonly ITracingService _tracingService;

        /// <summary>
        /// Name of the plugin, included in all log entries for identification.
        /// </summary>
        private readonly string _pluginName;

        /// <summary>
        /// Stopwatch for measuring elapsed time since tracer initialization (plugin start).
        /// </summary>
        private readonly Stopwatch _stopwatch;

        /// <summary>
        /// Flag indicating whether tracing is enabled. When false, most logs are suppressed to reduce verbosity.
        /// </summary>
        private bool _isEnabled;

        /// <summary>
        /// Initializes a new instance of the <see cref="PluginTracer"/> class and starts the performance stopwatch.
        /// </summary>
        /// <param name="tracingService">The Dataverse tracing service. Cannot be null.</param>
        /// <param name="pluginName">The name of the plugin, used in log formatting for identification.</param>
        /// <exception cref="ArgumentNullException">Thrown if tracingService is null.</exception>
        /// <remarks>
        /// The stopwatch starts immediately upon construction and runs for the lifetime of the tracer instance.
        /// Tracing is enabled by default; call <see cref="SetTracingEnabled"/> to disable it.
        /// </remarks>
        public PluginTracer(ITracingService tracingService, string pluginName)
        {
            _tracingService = tracingService ?? throw new ArgumentNullException(nameof(tracingService));
            _pluginName = pluginName;
            _stopwatch = Stopwatch.StartNew();
            _isEnabled = true; // Default enabled
        }

        /// <summary>
        /// Enables or disables tracing output. When disabled, most log methods are suppressed except errors.
        /// </summary>
        /// <param name="isEnabled">
        /// <c>true</c> to enable tracing (log Info, Warning, Debug, and Error messages);
        /// <c>false</c> to disable tracing (only Error messages are logged).
        /// </param>
        /// <remarks>
        /// This setting is typically controlled by the cascade configuration's EnableTracing property.
        /// Disable tracing in production environments to reduce plugin trace log verbosity and improve performance.
        /// </remarks>
        public void SetTracingEnabled(bool isEnabled)
        {
            _isEnabled = isEnabled;
        }

        /// <summary>
        /// Gets a value indicating whether tracing is currently enabled.
        /// </summary>
        /// <value>
        /// <c>true</c> if tracing is enabled; otherwise, <c>false</c>.
        /// </value>
        public bool IsEnabled => _isEnabled;

        /// <summary>
        /// Logs an informational message at INFO level. Only written if tracing is enabled.
        /// </summary>
        /// <param name="message">The informational message to log.</param>
        /// <remarks>
        /// Use for general progress updates, configuration summaries, and normal execution flow information.
        /// Suppressed when <see cref="IsEnabled"/> is false.
        /// </remarks>
        public void Info(string message)
        {
            if (_isEnabled)
            {
                Log("INFO", message);
            }
        }

        /// <summary>
        /// Logs a warning message at WARNING level. Only written if tracing is enabled.
        /// </summary>
        /// <param name="message">The warning message to log.</param>
        /// <remarks>
        /// Use for non-critical issues that don't prevent execution but should be investigated.
        /// Examples: unexpected stage, missing pre-image, metadata retrieval failures, value truncation.
        /// Suppressed when <see cref="IsEnabled"/> is false.
        /// </remarks>
        public void Warning(string message)
        {
            if (_isEnabled)
            {
                Log("WARNING", message);
            }
        }

        /// <summary>
        /// Logs an error message at ERROR level, optionally including exception details.
        /// Always written regardless of tracing enabled state for critical error visibility.
        /// </summary>
        /// <param name="message">A descriptive error message explaining what failed.</param>
        /// <param name="ex">Optional exception object. If provided, includes exception type, message, and stack trace in the log.</param>
        /// <remarks>
        /// <para><b>Error Format with Exception:</b></para>
        /// <code>Message | Exception: ExceptionType - Exception Message | StackTrace: stack trace details</code>
        ///
        /// <para><b>Always Logged:</b></para>
        /// Error logs are written even when <see cref="IsEnabled"/> is false, ensuring critical failures
        /// are always visible in the plugin trace log for troubleshooting.
        /// </remarks>
        public void Error(string message, Exception ex = null)
        {
            if (ex != null)
            {
                Log("ERROR", $"{message} | Exception: {ex.GetType().Name} - {ex.Message} | StackTrace: {ex.StackTrace}");
            }
            else
            {
                Log("ERROR", message);
            }
        }

        /// <summary>
        /// Logs a debug message at DEBUG level for detailed diagnostic information. Only written if tracing is enabled.
        /// </summary>
        /// <param name="message">The debug message to log.</param>
        /// <remarks>
        /// Use for detailed execution flow, field value inspections, query details, and other verbose diagnostic information.
        /// Suppressed when <see cref="IsEnabled"/> is false to reduce log verbosity in production.
        /// </remarks>
        public void Debug(string message)
        {
            if (_isEnabled)
            {
                Log("DEBUG", message);
            }
        }

        /// <summary>
        /// Logs the start of a named operation. Use with <see cref="EndOperation"/> to measure operation duration.
        /// </summary>
        /// <param name="operationName">The name of the operation being started (e.g., "CascadeFieldValues", "RetrieveRelatedRecords").</param>
        /// <remarks>
        /// Logs an INFO-level message: "Starting operation: {operationName}".
        /// Pair with <see cref="EndOperation"/> using the same operation name to measure elapsed time.
        /// Suppressed when <see cref="IsEnabled"/> is false.
        /// </remarks>
        public void StartOperation(string operationName)
        {
            if (_isEnabled)
            {
                Info($"Starting operation: {operationName}");
            }
        }

        /// <summary>
        /// Logs the completion of a named operation along with the total elapsed time since tracer initialization.
        /// </summary>
        /// <param name="operationName">The name of the operation being completed (should match the corresponding <see cref="StartOperation"/> call).</param>
        /// <remarks>
        /// Logs an INFO-level message: "Completed operation: {operationName} | Elapsed: {milliseconds}ms".
        /// The elapsed time represents total time since the plugin started (tracer created), not just the operation duration.
        /// For operation-specific timing, you would need to track start times separately.
        /// Suppressed when <see cref="IsEnabled"/> is false.
        /// </remarks>
        public void EndOperation(string operationName)
        {
            if (_isEnabled)
            {
                Info($"Completed operation: {operationName} | Elapsed: {_stopwatch.ElapsedMilliseconds}ms");
            }
        }

        /// <summary>
        /// Logs detailed information about the plugin execution context for troubleshooting and diagnostics.
        /// </summary>
        /// <param name="context">The plugin execution context containing message, entity, stage, user, and correlation information.</param>
        /// <remarks>
        /// <para><b>Logged Information:</b></para>
        /// <list type="bullet">
        ///     <item><description>Message name, stage, and mode (sync/async)</description></item>
        ///     <item><description>Primary entity name and ID</description></item>
        ///     <item><description>Executing user ID and organization name</description></item>
        ///     <item><description>Execution depth (for recursion detection) and correlation ID (for request tracing)</description></item>
        /// </list>
        ///
        /// <para><b>Usage:</b></para>
        /// Call this method early in plugin execution to capture context details in the trace log.
        /// Very helpful for troubleshooting issues in production where you can't debug interactively.
        /// </remarks>
        public void LogContextDetails(IPluginExecutionContext context)
        {
            if (context == null) return;

            Info($"Execution Context - Message: {context.MessageName} | Stage: {context.Stage} | Mode: {context.Mode}");
            Info($"Primary Entity: {context.PrimaryEntityName} | Primary Entity Id: {context.PrimaryEntityId}");
            Info($"User Id: {context.UserId} | Organization: {context.OrganizationName}");
            Info($"Depth: {context.Depth} | Correlation Id: {context.CorrelationId}");
        }

        private void Log(string level, string message)
        {
            var timestamp = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var elapsed = _stopwatch.ElapsedMilliseconds;
            _tracingService.Trace($"[{timestamp}] [{level}] [{_pluginName}] [+{elapsed}ms] {message}");
        }
    }
}
