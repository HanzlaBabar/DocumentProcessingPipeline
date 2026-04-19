using DocumentProcessingPipeline.Application.Interfaces;
using DocumentProcessingPipeline.Application.OCR;
using DocumentProcessingPipeline.Core.Enums;
using DocumentProcessingPipeline.Core.Events;
using DocumentProcessingPipeline.Core.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DocumentProcessingPipeline.Infrastructure.Services
{
    /// <summary>
    /// Background worker that consumes document processing events from Kafka
    /// and orchestrates the processing pipeline (OCR, tag detection, storage).
    /// </summary>
    public class DocumentProcessingWorker : BackgroundService
    {
        private readonly IEventConsumer _eventConsumer;
        private readonly IEventProducer _eventProducer;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<DocumentProcessingWorker> _logger;

        public DocumentProcessingWorker(
            IEventConsumer eventConsumer,
            IEventProducer eventProducer,
            IServiceScopeFactory scopeFactory,
            ILogger<DocumentProcessingWorker> logger)
        {
            _eventConsumer = eventConsumer ?? throw new ArgumentNullException(nameof(eventConsumer));
            _eventProducer = eventProducer ?? throw new ArgumentNullException(nameof(eventProducer));
            _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Document Processing Worker started");

            try
            {
                await _eventConsumer.StartConsumingAsync(ProcessDocumentAsync, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Document Processing Worker shutdown initiated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Document Processing Worker terminated unexpectedly");
                throw;
            }
        }

        private async Task ProcessDocumentAsync(DocumentProcessingEvent documentEvent, CancellationToken cancellationToken)
        {
            if (documentEvent == null)
            {
                _logger.LogWarning("Received null document processing event");
                return;
            }

            _logger.LogInformation("Processing document: {DocumentId}, FileName: {FileName}",
                documentEvent.DocumentId, documentEvent.FileName);

            using var scope = _scopeFactory.CreateScope();

            try
            {
                var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
                var ocrService = scope.ServiceProvider.GetRequiredService<OcrService>();
                var tagService = scope.ServiceProvider.GetRequiredService<ITagDetectionService>();

                // Get document from database
                var document = await repository.GetByIdAsync(documentEvent.DocumentId);

                if (document == null)
                {
                    _logger.LogWarning("Document not found: {DocumentId}", documentEvent.DocumentId);
                    await _eventProducer.PublishToDeadLetterQueueAsync(
                        documentEvent,
                        "Document not found in database",
                        cancellationToken);
                    return;
                }

                // Update status to processing
                document.Status = DocumentStatus.Processing;
                await repository.UpdateAsync(document);

                // Step 1: OCR
                _logger.LogInformation("Starting OCR for document: {DocumentId}", documentEvent.DocumentId);
                var text = ocrService.ExtractText(documentEvent.FilePath);

                // Step 2: Tag Detection
                _logger.LogInformation("Starting tag detection for document: {DocumentId}", documentEvent.DocumentId);
                var tags = tagService.DetectTags(text);

                // Update document with results
                document.Tags = tags;
                document.Status = DocumentStatus.Completed;
                await repository.UpdateAsync(document);

                _logger.LogInformation(
                    "Document processed successfully: {DocumentId}, Tags: {TagCount}",
                    documentEvent.DocumentId, tags.Count);
            }
            catch (InvalidDocumentException ex)
            {
                _logger.LogWarning(ex, "Invalid document: {DocumentId}", documentEvent.DocumentId);
                await HandleProcessingFailure(documentEvent, ex.Message, scope, cancellationToken);
            }
            catch (OcrProcessingException ex)
            {
                _logger.LogError(ex, "OCR failed for document: {DocumentId}", documentEvent.DocumentId);

                // Increment retry count
                documentEvent.RetryCount++;

                if (documentEvent.RetryCount < 3)
                {
                    // Republish for retry with exponential backoff
                    _logger.LogInformation("Retrying document processing: {DocumentId}, Attempt: {Attempt}",
                        documentEvent.DocumentId, documentEvent.RetryCount);
                    await _eventProducer.PublishDocumentProcessingEventAsync(documentEvent, cancellationToken);
                }
                else
                {
                    // Max retries exceeded
                    await HandleProcessingFailure(documentEvent, ex.Message, scope, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error processing document: {DocumentId}", documentEvent.DocumentId);
                await HandleProcessingFailure(documentEvent, ex.Message, scope, cancellationToken);
            }
        }

        private async Task HandleProcessingFailure(
            DocumentProcessingEvent documentEvent,
            string errorMessage,
            IServiceScope scope,
            CancellationToken cancellationToken)
        {
            try
            {
                var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();
                var document = await repository.GetByIdAsync(documentEvent.DocumentId);

                if (document != null)
                {
                    document.Status = DocumentStatus.Failed;
                    await repository.UpdateAsync(document);
                }

                // Send to dead letter queue for investigation
                await _eventProducer.PublishToDeadLetterQueueAsync(
                    documentEvent,
                    errorMessage,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling processing failure for document: {DocumentId}",
                    documentEvent.DocumentId);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Stopping Document Processing Worker");
            await base.StopAsync(cancellationToken);
        }
    }
}