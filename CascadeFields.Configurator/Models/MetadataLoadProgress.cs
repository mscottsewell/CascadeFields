using System;

namespace CascadeFields.Configurator.Models
{
    /// <summary>
    /// Represents progress information during metadata loading operations.
    /// Used with IProgress&lt;MetadataLoadProgress&gt; to report status to the UI.
    /// </summary>
    public class MetadataLoadProgress
    {
        /// <summary>
        /// Gets or sets the number of items completed.
        /// </summary>
        public int Completed { get; set; }

        /// <summary>
        /// Gets or sets the total number of items to process.
        /// </summary>
        public int Total { get; set; }

        /// <summary>
        /// Gets or sets an optional status message describing the current operation.
        /// </summary>
        public string? Message { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MetadataLoadProgress"/> class.
        /// </summary>
        public MetadataLoadProgress() { }

        /// <summary>
        /// Initializes a new instance of the <see cref="MetadataLoadProgress"/> class with progress values.
        /// </summary>
        /// <param name="completed">Number of items completed.</param>
        /// <param name="total">Total number of items.</param>
        /// <param name="message">Optional status message.</param>
        public MetadataLoadProgress(int completed, int total, string? message = null)
        {
            Completed = completed;
            Total = total;
            Message = message;
        }
    }
}
