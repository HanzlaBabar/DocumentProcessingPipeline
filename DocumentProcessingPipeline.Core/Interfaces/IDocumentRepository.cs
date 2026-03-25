using DocumentProcessingPipeline.Core.Entities;

namespace DocumentProcessingPipeline.Core.Interfaces
{
    public interface IDocumentRepository
    {
        Task CreateAsync(Document document);
        Task<Document?> GetByIdAsync(string id);
        Task<List<Document>> GetAllAsync();
        Task UpdateAsync(Document document);
    }
}
