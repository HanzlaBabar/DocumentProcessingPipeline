using DocumentProcessingPipeline.Core.Entities;
using DocumentProcessingPipeline.Core.Exceptions;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace DocumentProcessingPipeline.Infrastructure.Context
{
    public class MongoDbContext : IDisposable
    {
        private readonly IMongoDatabase _database;
        private readonly IMongoClient _client;
        private readonly ILogger<MongoDbContext> _logger;

        public MongoDbContext(string connectionString, string dbName, ILogger<MongoDbContext> logger)
        {
            _logger = logger;

            try
            {
                // Validation
                if (string.IsNullOrWhiteSpace(connectionString))
                {
                    throw new InvalidOperationException("MongoDB connection string is required");
                }

                if (string.IsNullOrWhiteSpace(dbName))
                {
                    throw new InvalidOperationException("Database name is required");
                }

                _logger.LogInformation("Initializing MongoDB context with database: {DatabaseName}", dbName);

                _client = new MongoClient(connectionString);

                if (_client == null)
                {
                    throw new DocumentProcessingException(
                        "Failed to create MongoDB client",
                        "MONGODB_CONNECTION_FAILED");
                }

                _database = _client.GetDatabase(dbName);

                if (_database == null)
                {
                    throw new DocumentProcessingException(
                        $"Failed to get database: {dbName}",
                        "MONGODB_DATABASE_FAILED");
                }

                // Verify connection with a simple ping
                VerifyConnection();

                _logger.LogInformation("MongoDB context initialized successfully");
            }
            catch (MongoConnectionException ex)
            {
                _logger.LogError(ex, "Failed to connect to MongoDB");
                throw new DocumentProcessingException(
                    "Cannot connect to MongoDB. Please verify the connection string and that MongoDB is running.",
                    ex,
                    "MONGODB_CONNECTION_FAILED");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing MongoDB context");
                throw;
            }
        }

        private void VerifyConnection()
        {
            try
            {
                var admin = _database.Client.GetDatabase("admin");
                var result = admin.RunCommand<BsonDocument>(new BsonDocument("ping", 1));
                _logger.LogInformation("MongoDB ping successful");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MongoDB connection verification failed");
                throw new DocumentProcessingException(
                    "MongoDB is not responding to ping",
                    ex,
                    "MONGODB_PING_FAILED");
            }
        }

        public IMongoCollection<Document> Documents =>
            GetCollection<Document>("Documents");

        private IMongoCollection<T> GetCollection<T>(string collectionName)
        {
            if (string.IsNullOrWhiteSpace(collectionName))
            {
                throw new ArgumentException("Collection name cannot be null or empty", nameof(collectionName));
            }

            return _database.GetCollection<T>(collectionName);
        }

        public void Dispose()
        {
            _client?.Dispose();
            _logger.LogInformation("MongoDB context disposed");
        }
    }
}