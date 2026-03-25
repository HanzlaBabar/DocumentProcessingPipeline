using DocumentProcessingPipeline.Core.Entities;

namespace DocumentProcessingPipeline.Core.Interfaces
{
    public interface IDocumentService
    {
        Task<Document> UploadAsync(string fileName, string filePath);
    }
}
