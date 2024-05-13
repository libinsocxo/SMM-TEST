using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Socxo_Smm_Backend.Core.Model
{
    public class Page
    {
        [BsonId]
        public ObjectId Id { get; set; }

        [BsonElement]
        public string? PageId { get; set; }

        [BsonElement]
        public string? PageRole { get; set; }

    }
}
