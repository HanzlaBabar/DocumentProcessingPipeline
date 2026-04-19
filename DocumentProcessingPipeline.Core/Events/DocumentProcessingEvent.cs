
namespace DocumentProcessingPipeline.Core.Events
{
    /// <summary>
    /// Event published to Kafka when a document needs processing.
    /// This is the contract for the event-driven pipeline.
    /// </summary>
    public class DocumentProcessingEvent
    {
        public string DocumentId { get; set; } = Guid.NewGuid().ToString();

        public string FileName { get; set; }

        public string FilePath { get; set; }

        /// <summary>
        /// Timestamp when the event was created (for audit trail)
        /// </summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Number of retry attempts (for dead letter queue)
        /// </summary>
        public int RetryCount { get; set; } = 0;

        /// <summary>
        /// Last error message (if failed)
        /// </summary>
        public string? LastError { get; set; }

        public DocumentProcessingEvent() { }

        public DocumentProcessingEvent(string documentId, string fileName, string filePath)
        {
            DocumentId = documentId;
            FileName = fileName;
            FilePath = filePath;
            CreatedAt = DateTime.UtcNow;
        }
    }
}