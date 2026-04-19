using DocumentProcessingPipeline.Application.Interfaces;
using DocumentProcessingPipeline.Core.Entities;
using DocumentProcessingPipeline.Core.Exceptions;
using DocumentProcessingPipeline.Infrastructure.Context;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace DocumentProcessingPipeline.Infrastructure.Repositories
{
    public class DocumentRepository : IDocumentRepository
    {
        private readonly IMongoCollection<Document> _documents;
        private readonly ILogger<DocumentRepository> _logger;

        public DocumentRepository(MongoDbContext context, ILogger<DocumentRepository> logger)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            _logger = logger;
            _documents = context.Documents;

            if (_documents == null)
            {
                throw new DocumentProcessingException(
                    "Failed to get Documents collection",
                    "MONGODB_COLLECTION_FAILED");
            }
        }

        public async Task CreateAsync(Document document)
        {
            try
            {
                if (document == null)
                {
                    throw new ArgumentNullException(nameof(document));
                }

                if (string.IsNullOrWhiteSpace(document.FileName))
                {
                    throw new InvalidDocumentException("Document file name is required");
                }

                _logger.LogInformation("Creating document: {DocumentId}", document.Id);

                await _documents.InsertOneAsync(document);

                _logger.LogInformation("Document created successfully: {DocumentId}", document.Id);
            }
            catch (MongoWriteException ex)
            {
                _logger.LogError(ex, "MongoDB write error creating document: {DocumentId}", document.Id);
                throw new DocumentProcessingException(
                    "Failed to save document to database",
                    ex,
                    "DATABASE_WRITE_FAILED");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error creating document: {DocumentId}", document?.Id);
                throw new DocumentProcessingException(
                    $"Error creating document: {ex.Message}",
                    ex,
                    "DATABASE_ERROR");
            }
        }

        public async Task<Document?> GetByIdAsync(string id)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(id))
                {
                    _logger.LogWarning("GetByIdAsync called with empty id");
                    return null;
                }

                _logger.LogDebug("Retrieving document: {DocumentId}", id);

                var document = await _documents.Find(d => d.Id == id).FirstOrDefaultAsync();

                if (document == null)
                {
                    _logger.LogWarning("Document not found: {DocumentId}", id);
                }
                else
                {
                    _logger.LogDebug("Document retrieved successfully: {DocumentId}", id);
                }

                return document;
            }
            catch (MongoException ex)
            {
                _logger.LogError(ex, "MongoDB error retrieving document: {DocumentId}", id);
                throw new DocumentProcessingException(
                    "Failed to retrieve document from database",
                    ex,
                    "DATABASE_READ_FAILED");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error retrieving document: {DocumentId}", id);
                throw new DocumentProcessingException(
                    $"Error retrieving document: {ex.Message}",
                    ex,
                    "DATABASE_ERROR");
            }
        }

        public async Task<List<Document>> GetAllAsync()
        {
            try
            {
                _logger.LogDebug("Retrieving all documents");

                var documents = await _documents.Find(_ => true).ToListAsync();

                _logger.LogInformation("Retrieved {DocumentCount} documents", documents.Count);

                return documents ?? new List<Document>();
            }
            catch (MongoException ex)
            {
                _logger.LogError(ex, "MongoDB error retrieving documents");
                throw new DocumentProcessingException(
                    "Failed to retrieve documents from database",
                    ex,
                    "DATABASE_READ_FAILED");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error retrieving documents");
                throw new DocumentProcessingException(
                    $"Error retrieving documents: {ex.Message}",
                    ex,
                    "DATABASE_ERROR");
            }
        }

        public async Task UpdateAsync(Document document)
        {
            try
            {
                if (document == null)
                {
                    throw new ArgumentNullException(nameof(document));
                }

                if (string.IsNullOrWhiteSpace(document.Id))
                {
                    throw new InvalidDocumentException("Document ID is required for update");
                }

                _logger.LogInformation("Updating document: {DocumentId}", document.Id);

                var result = await _documents.ReplaceOneAsync(d => d.Id == document.Id, document);

                if (result.MatchedCount == 0)
                {
                    _logger.LogWarning("Document not found for update: {DocumentId}", document.Id);
                    throw new InvalidDocumentException($"Document not found: {document.Id}");
                }

                _logger.LogInformation("Document updated successfully: {DocumentId}", document.Id);
            }
            catch (MongoWriteException ex)
            {
                _logger.LogError(ex, "MongoDB write error updating document: {DocumentId}", document.Id);
                throw new DocumentProcessingException(
                    "Failed to update document in database",
                    ex,
                    "DATABASE_WRITE_FAILED");
            }
            catch (InvalidDocumentException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error updating document: {DocumentId}", document?.Id);
                throw new DocumentProcessingException(
                    $"Error updating document: {ex.Message}",
                    ex,
                    "DATABASE_ERROR");
            }
        }
    }
}