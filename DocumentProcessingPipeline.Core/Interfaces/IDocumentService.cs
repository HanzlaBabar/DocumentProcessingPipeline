using DocumentProcessingPipeline.Core.Entities;

namespace DocumentProcessingPipeline.Core.Interfaces
{
    public interface IDocumentService
    {
        Task<Document> UploadAndProcessAsync(string fileName, string filePath);
    }
}
