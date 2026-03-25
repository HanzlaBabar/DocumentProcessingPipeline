using DocumentProcessingPipeline.Core.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DocumentProcessingPipeline.Core.Entities
{
    public class Document
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string FileName { get; set; }

        public string FilePath { get; set; }

        [BsonRepresentation(BsonType.String)]
        public DocumentStatus Status { get; set; } = DocumentStatus.Uploaded;

        public List<Tag> Tags { get; set; } = new();
    }
}
