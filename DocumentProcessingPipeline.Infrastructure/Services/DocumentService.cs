
using DocumentProcessingPipeline.Core.Entities;
using DocumentProcessingPipeline.Core.Enums;
using DocumentProcessingPipeline.Core.Interfaces;

namespace DocumentProcessingPipeline.Infrastructure.Services
{
    public class DocumentService : IDocumentService
    {
        private readonly IDocumentRepository _repository;
        private readonly IProcessingQueue _queue;

        public DocumentService(IDocumentRepository repository, IProcessingQueue queue)
        {
            _repository = repository;
            _queue = queue;
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

            //trigger async processing
            _queue.Enqueue(document.Id);

            return document;
        }
    }
}
