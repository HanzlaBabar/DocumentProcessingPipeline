using Confluent.Kafka;
using DocumentProcessingPipeline.Application.Interfaces;
using DocumentProcessingPipeline.Core.Events;
using Microsoft.Extensions.Logging;
using SharpCompress.Common;
using System.Collections.Concurrent;
using System.Text.Json;

namespace DocumentProcessingPipeline.Infrastructure.Kafka
{
    /// <summary>
    /// Kafka-based implementation of IEventProducer.
    /// Publishes document processing events to Kafka topics for distributed processing.
    /// </summary>
    public class KafkaEventProducer : IEventProducer, IAsyncDisposable
    {
        private readonly IProducer<string, string> _producer;
        private readonly ILogger<KafkaEventProducer> _logger;
        private const string ProcessingTopic = "document-processing-events";
        private const string DeadLetterTopic = "document-processing-dlq";

        public KafkaEventProducer(string bootstrapServers, ILogger<KafkaEventProducer> logger)
        {
            if (string.IsNullOrWhiteSpace(bootstrapServers))
            {
                throw new ArgumentException("Bootstrap servers cannot be null or empty", nameof(bootstrapServers));
            }

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            try
            {
                var config = new ProducerConfig
                {
                    BootstrapServers = bootstrapServers,
                    Acks = Acks.All, // Wait for all replicas to acknowledge (exactly-once semantics)
                    CompressionType = Confluent.Kafka.CompressionType.Snappy, // Compress messages for efficiency
                    EnableIdempotence = true, // Prevent duplicate messages
                    MaxInFlight = 5,
                    Partitioner = Confluent.Kafka.Partitioner.Consistent, // Use document ID for consistent partitioning
                };

                _producer = new ProducerBuilder<string, string>(config)
                    .SetErrorHandler((_, error) =>
                    {
                        _logger.LogError("Kafka producer error: {Error}", error.Reason);
                    })
                    .SetLogHandler((_, message) =>
                    {
                        _logger.LogDebug("Kafka: {Message}", message.Message);
                    })
                    .Build();

                _logger.LogInformation("Kafka producer initialized with bootstrap servers: {BootstrapServers}",
                    bootstrapServers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Kafka producer");
                throw;
            }
        }

        public async Task PublishDocumentProcessingEventAsync(
            DocumentProcessingEvent documentEvent,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (documentEvent == null)
                {
                    throw new ArgumentNullException(nameof(documentEvent));
                }

                if (string.IsNullOrWhiteSpace(documentEvent.DocumentId))
                {
                    throw new ArgumentException("DocumentId cannot be null or empty", nameof(documentEvent));
                }

                _logger.LogInformation("Publishing document processing event: {DocumentId}", documentEvent.DocumentId);

                var eventJson = JsonSerializer.Serialize(documentEvent);

                var message = new Message<string, string>
                {
                    Key = documentEvent.DocumentId, // Partition by document ID for ordering
                    Value = eventJson
                };

                var deliveryReport = await _producer.ProduceAsync(ProcessingTopic, message, cancellationToken);

                if (deliveryReport.Status == PersistenceStatus.NotPersisted)
                {
                    _logger.LogError("Failed to publish event to Kafka: {DocumentId}", documentEvent.DocumentId);
                    throw new InvalidOperationException(
                        $"Failed to publish event for document {documentEvent.DocumentId}");
                }

                _logger.LogInformation(
                    "Document processing event published successfully: {DocumentId}, Partition: {Partition}, Offset: {Offset}",
                    documentEvent.DocumentId, deliveryReport.Partition, deliveryReport.Offset);
            }
            catch (ProduceException<string, string> ex)
            {
                _logger.LogError(ex, "Kafka produce error for document: {DocumentId}", documentEvent?.DocumentId);
                throw new InvalidOperationException(
                    "Failed to publish event to Kafka. Ensure Kafka is running and accessible.",
                    ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error publishing event: {DocumentId}", documentEvent?.DocumentId);
                throw;
            }
        }

        public async Task PublishToDeadLetterQueueAsync(
            DocumentProcessingEvent documentEvent,
            string errorReason,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (documentEvent == null)
                {
                    throw new ArgumentNullException(nameof(documentEvent));
                }

                _logger.LogWarning(
                    "Publishing document to dead letter queue: {DocumentId}, Reason: {Reason}",
                    documentEvent.DocumentId, errorReason);

                documentEvent.LastError = errorReason;

                var eventJson = JsonSerializer.Serialize(documentEvent);

                var message = new Message<string, string>
                {
                    Key = documentEvent.DocumentId,
                    Value = eventJson
                };

                var deliveryReport = await _producer.ProduceAsync(DeadLetterTopic, message, cancellationToken);

                _logger.LogInformation(
                    "Event published to DLQ: {DocumentId}, Partition: {Partition}, Offset: {Offset}",
                    documentEvent.DocumentId, deliveryReport.Partition, deliveryReport.Offset);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing to dead letter queue: {DocumentId}", documentEvent?.DocumentId);
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _logger.LogInformation("Flushing and disposing Kafka producer");
                _producer?.Flush(TimeSpan.FromSeconds(10));
                _producer?.Dispose();
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing Kafka producer");
            }
        }
    }
}