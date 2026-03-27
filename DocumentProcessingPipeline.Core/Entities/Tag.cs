using DocumentProcessingPipeline.Core.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DocumentProcessingPipeline.Core.Entities
{
    public class Tag
    {
        public string Label { get; set; }

        [BsonRepresentation(BsonType.String)]
        public TagType Type { get; set; }
    }
}
