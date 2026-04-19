using DocumentProcessingPipeline.Core.Events;

namespace DocumentProcessingPipeline.Application.Interfaces
{
    /// <summary>
    /// Abstraction for consuming events from the message broker.
    /// Allows for different implementations and testing.
    /// </summary>
    public interface IEventConsumer
    {
        /// <summary>
        /// Starts consuming messages from the processing topic.
        /// This should be called in a background service.
        /// </summary>
        Task StartConsumingAsync(
            Func<DocumentProcessingEvent, CancellationToken, Task> messageHandler,
            CancellationToken cancellationToken);
    }
}