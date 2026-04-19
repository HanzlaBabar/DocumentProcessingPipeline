using DocumentProcessingPipeline.Core.Entities;

namespace DocumentProcessingPipeline.Application.Interfaces
{
    public interface IDocumentService
    {
        Task<Document> UploadAndProcessAsync(string fileName, string filePath);
    }
}
