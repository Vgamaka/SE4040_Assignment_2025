using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace backend.Models
{
    /// <summary>
    /// Represents a charging session created when an operator finalizes a booking.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class Session
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }   // MongoDB _id

        // Links the session to a specific booking & station
        [BsonElement("bookingId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? BookingId { get; set; }

        [BsonElement("stationId")]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? StationId { get; set; }

        // Optional: the operator (user) who finalized the session
        [BsonElement("operatorUserId")]
        public string? OperatorUserId { get; set; }

        // Timestamps (UTC)
        [BsonElement("startedAtUtc")]
        public DateTime StartedAtUtc { get; set; } = DateTime.UtcNow;

        [BsonElement("endedAtUtc")]
        public DateTime? EndedAtUtc { get; set; }

        // Simple lifecycle; can be extended later
        [BsonElement("status")]
        public string Status { get; set; } = "Completed"; // e.g., "Completed", "Aborted"

        // Optional metrics (extend as needed)
        [BsonElement("energyKWh")]
        public double? EnergyKWh { get; set; }

        [BsonElement("notes")]
        public string? Notes { get; set; }
    }
}
