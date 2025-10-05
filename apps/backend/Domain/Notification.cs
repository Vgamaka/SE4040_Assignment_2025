using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EvCharge.Api.Domain
{
    public class Notification
    {
        [BsonId, BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        // e.g., "BookingApproved","BookingRejected","BookingCancelled","CheckIn","Completed"
        [BsonElement("type")]
        public string Type { get; set; } = string.Empty;

        // who should see this (Owner NIC or staff NIC)
        [BsonElement("toNic")]
        public string ToNic { get; set; } = string.Empty;

        [BsonElement("subject")]
        public string Subject { get; set; } = string.Empty;

        [BsonElement("message")]
        public string Message { get; set; } = string.Empty;

        [BsonElement("payload")]
        public BsonDocument? Payload { get; set; }

        [BsonElement("createdAtUtc")]
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [BsonElement("sentUtc")]
        public DateTime? SentUtc { get; set; } // stub: we don't actually send
    }
}
