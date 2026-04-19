using Microsoft.Extensions.Logging;
using Tesseract;
using DocumentProcessingPipeline.Core.Exceptions;

namespace DocumentProcessingPipeline.Application.OCR
{
    public class OcrService
    {
        private readonly ILogger<OcrService> _logger;
        private const long MaxImageSize = 100 * 1024 * 1024; // 100MB

        public OcrService(ILogger<OcrService> logger)
        {
            _logger = logger;
        }

        public string ExtractText(string imagePath)
        {
            try
            {
                // Validation
                if (string.IsNullOrWhiteSpace(imagePath))
                {
                    throw new InvalidDocumentException("Image path cannot be null or empty");
                }

                if (!File.Exists(imagePath))
                {
                    _logger.LogError("Image file not found: {ImagePath}", imagePath);
                    throw new InvalidDocumentException($"Image file not found: {imagePath}");
                }

                var fileInfo = new FileInfo(imagePath);
                if (fileInfo.Length == 0)
                {
                    _logger.LogWarning("Image file is empty: {ImagePath}", imagePath);
                    throw new InvalidDocumentException("Image file is empty");
                }

                if (fileInfo.Length > MaxImageSize)
                {
                    _logger.LogWarning("Image file exceeds size limit: {ImagePath}, Size: {Size}",
                        imagePath, fileInfo.Length);
                    throw new InvalidDocumentException($"Image size exceeds {MaxImageSize / 1024 / 1024}MB limit");
                }

                _logger.LogInformation("Starting OCR extraction: {ImagePath}", imagePath);

                var tessDataPath = Path.Combine(AppContext.BaseDirectory, "tessdata");

                if (!Directory.Exists(tessDataPath))
                {
                    _logger.LogError("Tesseract data directory not found: {TessDataPath}", tessDataPath);
                    throw new OcrProcessingException($"Tesseract data not found at {tessDataPath}");
                }

                try
                {
                    using var engine = new TesseractEngine(tessDataPath, "eng", EngineMode.Default);

                    if (engine == null)
                    {
                        throw new OcrProcessingException("Failed to initialize Tesseract engine");
                    }

                    using var img = Pix.LoadFromFile(imagePath);

                    if (img == null)
                    {
                        _logger.LogError("Failed to load image: {ImagePath}", imagePath);
                        throw new OcrProcessingException($"Unable to load image file: {imagePath}");
                    }

                    using var page = engine.Process(img);

                    if (page == null)
                    {
                        throw new OcrProcessingException("Failed to process image with Tesseract");
                    }

                    var extractedText = page.GetText();

                    if (string.IsNullOrWhiteSpace(extractedText))
                    {
                        _logger.LogWarning("OCR returned empty text for: {ImagePath}", imagePath);
                        return "";
                    }

                    _logger.LogInformation("OCR completed successfully: {ImagePath}, TextLength: {Length}",
                        imagePath, extractedText.Length);

                    return extractedText;
                }
                catch (OutOfMemoryException ex)
                {
                    _logger.LogError(ex, "Out of memory during OCR processing: {ImagePath}", imagePath);
                    throw new OcrProcessingException("Out of memory - image may be too large", ex);
                }
            }
            catch (OcrProcessingException)
            {
                throw; // Re-throw custom exceptions
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during OCR processing: {ImagePath}", imagePath);
                throw new OcrProcessingException($"OCR processing failed: {ex.Message}", ex);
            }
        }
    }
}