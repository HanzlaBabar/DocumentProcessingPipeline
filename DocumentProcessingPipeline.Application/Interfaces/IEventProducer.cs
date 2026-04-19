using DocumentProcessingPipeline.Core.Events;

namespace DocumentProcessingPipeline.Application.Interfaces
{
    /// <summary>
    /// Abstraction for publishing events to the message broker.
    /// Allows switching from Kafka to other brokers without changing business logic.
    /// </summary>
    public interface IEventProducer
    {
        /// <summary>
        /// Publishes a document processing event to the broker.
        /// </summary>
        Task PublishDocumentProcessingEventAsync(DocumentProcessingEvent documentEvent, CancellationToken cancellationToken = default);

        /// <summary>
        /// Publishes an event to the dead letter queue for failed documents.
        /// </summary>
        Task PublishToDeadLetterQueueAsync(DocumentProcessingEvent documentEvent, string errorReason, CancellationToken cancellationToken = default);
    }
}