using DocumentProcessingPipeline.Core.Entities;
using DocumentProcessingPipeline.Core.Enums;
using DocumentProcessingPipeline.Core.Interfaces;
using DocumentProcessingPipeline.Infrastructure.OCR;

public class DocumentService : IDocumentService
{
    private readonly IDocumentRepository _repository;
    private readonly OcrService _ocrService;
    private readonly ITagDetectionService _tagService;

    public DocumentService(
        IDocumentRepository repository,
        OcrService ocrService,
        ITagDetectionService tagService)
    {
        _repository = repository;
        _ocrService = ocrService;
        _tagService = tagService;
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

        try
        {
            // OCR
            var text = _ocrService.ExtractText(filePath);

            Console.WriteLine($"OCR TEXT:\n{text}");

            // Tag detection
            var tags = _tagService.DetectTags(text);

            document.Tags = tags;
            document.Status = DocumentStatus.Completed;
        }
        catch
        {
            document.Status = DocumentStatus.Failed;
        }

        await _repository.UpdateAsync(document);

        return document;
    }
}