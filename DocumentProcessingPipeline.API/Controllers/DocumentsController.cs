using DocumentProcessingPipeline.Application.Interfaces;
using DocumentProcessingPipeline.Core.Events;
using DocumentProcessingPipeline.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DocumentProcessingPipeline.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentService _service;
        private readonly IEventProducer _eventProducer;
        private readonly ILogger<DocumentsController> _logger;
        private const long MaxFileSize = 50 * 1024 * 1024; // 50MB
        private static readonly string[] AllowedExtensions = { ".pdf", ".png", ".jpg", ".jpeg", ".tiff" };

        public DocumentsController(
            IDocumentService service,
            IEventProducer eventProducer,
            ILogger<DocumentsController> logger)
        {
            _service = service;
            _eventProducer = eventProducer;
            _logger = logger;
        }

        [HttpPost("upload")]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            try
            {
                // Validation
                if (file == null)
                {
                    _logger.LogWarning("Upload attempt with null file");
                    return BadRequest(new { error = "File is required" });
                }

                if (file.Length == 0)
                {
                    _logger.LogWarning("Upload attempt with empty file: {FileName}", file.FileName);
                    return BadRequest(new { error = "File is empty" });
                }

                if (file.Length > MaxFileSize)
                {
                    _logger.LogWarning("Upload attempt with oversized file: {FileName}, Size: {Size}",
                        file.FileName, file.Length);
                    return BadRequest(new { error = $"File size exceeds {MaxFileSize / 1024 / 1024}MB limit" });
                }

                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!AllowedExtensions.Contains(extension))
                {
                    _logger.LogWarning("Upload attempt with unsupported extension: {Extension}", extension);
                    return BadRequest(new { error = $"File type not supported. Allowed: {string.Join(", ", AllowedExtensions)}" });
                }

                // Save file
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
                Directory.CreateDirectory(uploadsFolder);

                var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
                var filePath = Path.Combine(uploadsFolder, uniqueFileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                _logger.LogInformation("File uploaded successfully: {FileName}, Path: {FilePath}",
                    file.FileName, filePath);

                // Save document to database
                var document = await _service.UploadAndProcessAsync(file.FileName, filePath);

                // Publish event to Kafka for async processing
                var processingEvent = new DocumentProcessingEvent(
                    document.Id,
                    document.FileName,
                    document.FilePath);

                await _eventProducer.PublishDocumentProcessingEventAsync(processingEvent);

                _logger.LogInformation("Document processing event published: {DocumentId}", document.Id);

                return Accepted(new
                {
                    documentId = document.Id,
                    fileName = document.FileName,
                    status = "queued",
                    message = "Document uploaded and queued for processing"
                });
            }
            catch (InvalidDocumentException ex)
            {
                _logger.LogWarning(ex, "Invalid document upload: {Message}", ex.Message);
                return BadRequest(new { error = ex.Message, errorCode = ex.ErrorCode });
            }
            catch (DocumentProcessingException ex)
            {
                _logger.LogError(ex, "Document processing failed: {Message}", ex.Message);
                return StatusCode(500, new { error = ex.Message, errorCode = ex.ErrorCode });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during file upload");
                return StatusCode(500, new { error = "An unexpected error occurred. Please try again." });
            }
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDocument(string id)
        {
            try
            {
                var document = await _service.GetDocumentAsync(id);
                if (document == null)
                {
                    _logger.LogWarning("Document not found: {DocumentId}", id);
                    return NotFound(new { error = "Document not found" });
                }

                return Ok(document);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving document: {DocumentId}", id);
                return StatusCode(500, new { error = "An error occurred while retrieving the document" });
            }
        }
    }
}