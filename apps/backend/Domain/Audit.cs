using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EvCharge.Api.Domain
{
    public class AuditEvent
    {
        [BsonId, BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        // e.g., "booking", "session"
        [BsonElement("entityType")]
        public string EntityType { get; set; } = string.Empty;

        [BsonElement("entityId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string EntityId { get; set; } = string.Empty;

        // e.g., "Approved", "Rejected", "Cancelled", "QrIssued", "CheckedIn", "Completed"
        [BsonElement("action")]
        public string Action { get; set; } = string.Empty;

        [BsonElement("actorNic")]
        public string? ActorNic { get; set; }

        [BsonElement("actorRole")]
        public string? ActorRole { get; set; }

        // free-form small payload
        [BsonElement("payload")]
        public BsonDocument? Payload { get; set; }

        [BsonElement("createdAtUtc")]
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }
}
