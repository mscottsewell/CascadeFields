using Microsoft.Xrm.Sdk;
using System;
using System.Diagnostics;

namespace CascadeFields.Plugin.Helpers
{
    /// <summary>
    /// Provides tracing and logging functionality for plugins
    /// </summary>
    public class PluginTracer
    {
        private readonly ITracingService _tracingService;
        private readonly string _pluginName;
        private readonly Stopwatch _stopwatch;

        public PluginTracer(ITracingService tracingService, string pluginName)
        {
            _tracingService = tracingService ?? throw new ArgumentNullException(nameof(tracingService));
            _pluginName = pluginName;
            _stopwatch = Stopwatch.StartNew();
        }

        /// <summary>
        /// Logs an informational message
        /// </summary>
        public void Info(string message)
        {
            Log("INFO", message);
        }

        /// <summary>
        /// Logs a warning message
        /// </summary>
        public void Warning(string message)
        {
            Log("WARNING", message);
        }

        /// <summary>
        /// Logs an error message
        /// </summary>
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
        /// Logs a debug message
        /// </summary>
        public void Debug(string message)
        {
            Log("DEBUG", message);
        }

        /// <summary>
        /// Logs the start of an operation
        /// </summary>
        public void StartOperation(string operationName)
        {
            Info($"Starting operation: {operationName}");
        }

        /// <summary>
        /// Logs the end of an operation with elapsed time
        /// </summary>
        public void EndOperation(string operationName)
        {
            Info($"Completed operation: {operationName} | Elapsed: {_stopwatch.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// Logs execution context details
        /// </summary>
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
