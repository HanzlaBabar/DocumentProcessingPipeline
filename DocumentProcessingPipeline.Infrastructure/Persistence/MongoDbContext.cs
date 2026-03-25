using DocumentProcessingPipeline.Core.Entities;
using MongoDB.Driver;

namespace DocumentProcessingPipeline.Infrastructure.Persistence
{
    public class MongoDbContext
    {
        private readonly IMongoDatabase _database;

        public MongoDbContext(string connectionString, string dbName)
        {
            var client = new MongoClient(connectionString);
            _database = client.GetDatabase(dbName);
        }

        public IMongoCollection<Document> Documents =>
            _database.GetCollection<Document>("Documents");
    }
}
