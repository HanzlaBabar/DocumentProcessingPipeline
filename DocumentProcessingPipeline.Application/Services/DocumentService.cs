using DocumentProcessingPipeline.Application.Interfaces;
using DocumentProcessingPipeline.Application.OCR;
using DocumentProcessingPipeline.Core.Entities;
using DocumentProcessingPipeline.Core.Enums;
using DocumentProcessingPipeline.Core.Exceptions;
using Microsoft.Extensions.Logging;

namespace DocumentProcessingPipeline.Application.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _repository;
        private readonly OcrService _ocrService;
        private readonly ITagDetectionService _tagService;
        private readonly ILogger<DocumentService> _logger;

        public DocumentService(
            IDocumentRepository repository,
            OcrService ocrService,
            ITagDetectionService tagService,
            ILogger<DocumentService> logger)
        {
            _repository = repository;
            _ocrService = ocrService;
            _tagService = tagService;
            _logger = logger;
        }

        public async Task<Document> UploadAndProcessAsync(string fileName, string filePath)
        {
            var document = new Document
            {
                FileName = fileName,
                FilePath = filePath,
                Status = DocumentStatus.Processing
            };

            await _repository.CreateAsync(document);
            _logger.LogInformation("Document created: {DocumentId}, FileName: {FileName}", document.Id, fileName);

            try
            {
                // Validate file exists
                if (!File.Exists(filePath))
                {
                    throw new InvalidDocumentException($"File not found at path: {filePath}");
                }

                // OCR
                _logger.LogInformation("Starting OCR for document: {DocumentId}", document.Id);
                var text = _ocrService.ExtractText(filePath);

                if (string.IsNullOrWhiteSpace(text))
                {
                    _logger.LogWarning("OCR returned empty text for document: {DocumentId}", document.Id);
                    text = "";
                }

                _logger.LogInformation("OCR completed for document: {DocumentId}, TextLength: {TextLength}",
                    document.Id, text.Length);

                // Tag detection
                _logger.LogInformation("Starting tag detection for document: {DocumentId}", document.Id);
                var tags = _tagService.DetectTags(text);

                _logger.LogInformation("Tag detection completed for document: {DocumentId}, TagCount: {TagCount}",
                    document.Id, tags.Count);

                document.Tags = tags;
                document.Status = DocumentStatus.Completed;

                await _repository.UpdateAsync(document);

                _logger.LogInformation("Document processing completed successfully: {DocumentId}", document.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing document: {DocumentId}", document.Id);

                document.Status = DocumentStatus.Failed;
                await _repository.UpdateAsync(document);

                // Re-throw as custom exception to preserve context
                throw new DocumentProcessingException(
                    $"Failed to process document: {ex.Message}",
                    ex,
                    "PROCESSING_FAILED");
            }

            return document;
        }

        public async Task<Document?> GetDocumentAsync(string id)
        {
            try
            {
                return await _repository.GetByIdAsync(id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document: {DocumentId}", id);
                throw;
            }
        }
    }
}