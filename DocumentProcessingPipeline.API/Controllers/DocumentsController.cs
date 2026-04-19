using DocumentProcessingPipeline.Application.Interfaces;
using DocumentProcessingPipeline.Core.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace DocumentProcessingPipeline.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DocumentsController : ControllerBase
    {
        private readonly IDocumentService _service;
        private readonly ILogger<DocumentsController> _logger;
        private const long MaxFileSize = 50 * 1024 * 1024; // 50MB
        private static readonly string[] AllowedExtensions = { ".pdf", ".png", ".jpg", ".jpeg", ".tiff" };

        public DocumentsController(IDocumentService service, ILogger<DocumentsController> logger)
        {
            _service = service;
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

                // Save file with unique name to prevent overwrites
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

                // Process document
                var document = await _service.UploadAndProcessAsync(file.FileName, filePath);

                _logger.LogInformation("Document processed successfully: {DocumentId}", document.Id);

                return Ok(new
                {
                    documentId = document.Id,
                    fileName = document.FileName,
                    status = document.Status.ToString(),
                    tagCount = document.Tags.Count
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