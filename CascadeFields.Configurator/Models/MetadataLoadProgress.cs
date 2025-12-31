using System;

namespace CascadeFields.Configurator.Models
{
    public class MetadataLoadProgress
    {
        public int Completed { get; set; }
        public int Total { get; set; }
        public string? Message { get; set; }

        public MetadataLoadProgress() { }

        public MetadataLoadProgress(int completed, int total, string? message = null)
        {
            Completed = completed;
            Total = total;
            Message = message;
        }
    }
}
