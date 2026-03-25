
using DocumentProcessingPipeline.Core.Entities;
using DocumentProcessingPipeline.Core.Enums;
using DocumentProcessingPipeline.Core.Interfaces;

namespace DocumentProcessingPipeline.Infrastructure.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _repository;

        public DocumentService(IDocumentRepository repository)
        {
            _repository = repository;
        }

        public async Task<Document> UploadAsync(string fileName, string filePath)
        {
            var document = new Document
            {
                FileName = fileName,
                FilePath = filePath,
                Status = DocumentStatus.Uploaded
            };

            await _repository.CreateAsync(document);

            return document;
        }
    }
}
