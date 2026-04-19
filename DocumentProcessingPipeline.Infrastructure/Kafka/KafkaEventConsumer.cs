using Confluent.Kafka;
using DocumentProcessingPipeline.Application.Interfaces;
using DocumentProcessingPipeline.Core.Events;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace DocumentProcessingPipeline.Infrastructure.Kafka
{
    /// <summary>
    /// Kafka-based implementation of IEventConsumer.
    /// Consumes document processing events from Kafka topic for processing.
    /// </summary>
    public class KafkaEventConsumer : IEventConsumer, IAsyncDisposable
    {
        private readonly IConsumer<string, string> _consumer;
        private readonly ILogger<KafkaEventConsumer> _logger;
        private const string ProcessingTopic = "document-processing-events";
        private const string ConsumerGroup = "document-processor-group";

        public KafkaEventConsumer(string bootstrapServers, ILogger<KafkaEventConsumer> logger)
        {
            if (string.IsNullOrWhiteSpace(bootstrapServers))
            {
                throw new ArgumentException("Bootstrap servers cannot be null or empty", nameof(bootstrapServers));
            }

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            try
            {
                var config = new ConsumerConfig
                {
                    BootstrapServers = bootstrapServers,
                    GroupId = ConsumerGroup,
                    AutoOffsetReset = AutoOffsetReset.Earliest, // Start from beginning if no offset saved
                    EnableAutoCommit = true,
                    AutoCommitIntervalMs = 5000,
                    SessionTimeoutMs = 30000,
                    HeartbeatIntervalMs = 10000,
                    IsolationLevel = IsolationLevel.ReadCommitted, // Only read committed messages
                };

                _consumer = new ConsumerBuilder<string, string>(config)
                    .SetErrorHandler((_, error) =>
                    {
                        _logger.LogError("Kafka consumer error: {Error}", error.Reason);
                    })
                    .SetPartitionsLostHandler((_, partitions) =>
                    {
                        _logger.LogWarning("Partitions lost: {Partitions}",
                            string.Join(", ", partitions));
                    })
                    .Build();

                _logger.LogInformation("Kafka consumer initialized with bootstrap servers: {BootstrapServers}",
                    bootstrapServers);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize Kafka consumer");
                throw;
            }
        }

        public async Task StartConsumingAsync(
            Func<DocumentProcessingEvent, CancellationToken, Task> messageHandler,
            CancellationToken cancellationToken)
        {
            if (messageHandler == null)
            {
                throw new ArgumentNullException(nameof(messageHandler));
            }

            try
            {
                _consumer.Subscribe(ProcessingTopic);
                _logger.LogInformation("Kafka consumer subscribed to topic: {Topic}", ProcessingTopic);

                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var consumeResult = _consumer.Consume(cancellationToken);

                        if (consumeResult.IsPartitionEOF)
                        {
                            _logger.LogDebug("Reached end of partition {Partition}", consumeResult.Partition);
                            continue;
                        }

                        if (consumeResult.Message == null)
                        {
                            continue;
                        }

                        try
                        {
                            var documentEvent = JsonSerializer.Deserialize<DocumentProcessingEvent>(
                                consumeResult.Message.Value);

                            if (documentEvent == null)
                            {
                                _logger.LogWarning("Failed to deserialize message from topic");
                                continue;
                            }

                            _logger.LogInformation(
                                "Processing event from Kafka: {DocumentId}, Partition: {Partition}, Offset: {Offset}",
                                documentEvent.DocumentId, consumeResult.Partition, consumeResult.Offset);

                            await messageHandler(documentEvent, cancellationToken);

                            // Commit offset after successful processing
                            _consumer.Commit(consumeResult);

                            _logger.LogDebug("Offset committed for document: {DocumentId}", documentEvent.DocumentId);
                        }
                        catch (JsonException ex)
                        {
                            _logger.LogError(ex, "Failed to deserialize Kafka message");
                            _consumer.Commit(consumeResult);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing message from Kafka");
                            // Don't commit offset - will retry on next poll
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogInformation("Consumer stopped");
                        break;
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Kafka consumer error: {Error}", ex.Error.Reason);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in consumer loop");
                throw;
            }
            finally
            {
                _consumer.Close();
                _logger.LogInformation("Kafka consumer closed");
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _logger.LogInformation("Disposing Kafka consumer");
                _consumer?.Dispose();
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing Kafka consumer");
            }
        }
    }
}