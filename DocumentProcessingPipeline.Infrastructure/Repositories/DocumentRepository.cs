using DocumentProcessingPipeline.Application.Interfaces;
using DocumentProcessingPipeline.Core.Entities;
using DocumentProcessingPipeline.Infrastructure.Persistence;
using MongoDB.Driver;

namespace DocumentProcessingPipeline.Infrastructure.Repositories
{
    public class DocumentRepository : IDocumentRepository
    {
        private readonly IMongoCollection<Document> _documents;

        public DocumentRepository (MongoDbContext context)
        {
            _documents = context.Documents;
        }

        public async Task CreateAsync(Document document)
        {
            await _documents.InsertOneAsync(document);
        }

        public async Task<Document?> GetByIdAsync(string id)
        {
            return await _documents.Find(d => d.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<Document>> GetAllAsync()
        {
            return await _documents.Find(_ => true).ToListAsync();
        }

        public async Task UpdateAsync(Document document)
        {
            await _documents.ReplaceOneAsync(d => d.Id == document.Id, document);
        }
    }
}
