// using MongoDB.Bson;
// using MongoDB.Bson.Serialization.Attributes;

// namespace backend.Models
// {
//     /// <summary>
//     /// Represents a charging slot booking made by an EV Owner.
//     /// </summary>
//     public class Booking
//     {
//         [BsonId]
//         [BsonRepresentation(BsonType.ObjectId)]
//         public string? Id { get; set; }   // ✅ Nullable → MongoDB generates this

//         [BsonElement("ownerNIC")]
//         public string? OwnerNIC { get; set; }  // FK → EvOwner NIC

//         [BsonElement("stationId")]
//         public string? StationId { get; set; } // FK → ChargingStation Id

//         [BsonElement("reservationDateTime")]
//         public DateTime ReservationDateTime { get; set; }

//         [BsonElement("createdAt")]
//         public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

//         [BsonElement("status")]
//         public string Status { get; set; } = "Pending";
//         // Pending, Approved, Cancelled, Completed

//         [BsonElement("qrCode")]
//         public string? QrCode { get; set; }  // Optional: generated after approval
//     }
// }
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace backend.Models
{
    /// <summary>
    /// Represents a charging slot booking made by an EV Owner.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class Booking
    {
        // MongoDB _id
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        // Foreign keys
        [BsonElement("ownerNIC")]
        public string? OwnerNIC { get; set; } // FK → EvOwner (NIC)

        [BsonElement("stationId")]
        public string? StationId { get; set; } // FK → ChargingStation.Id

        // === Time fields ===
        // NEW preferred fields (UTC) to support 7-day window and ≥12h modify/cancel rules.
        [BsonElement("startUtc")]
        public DateTime StartUtc { get; set; }  // Start time (UTC)

        [BsonElement("endUtc")]
        public DateTime EndUtc { get; set; }    // End time (UTC)

        // Legacy single datetime kept for backward compatibility with any existing data/UI.
        // If you still write this from the client, the controller/service can map it to StartUtc.
        [BsonElement("reservationDateTime")]
        public DateTime ReservationDateTime { get; set; }

        // Metadata
        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Status lifecycle: Pending, Approved, Cancelled, Completed
        [BsonElement("status")]
        public string Status { get; set; } = "Pending";

        // When the booking was approved (if applicable)
        [BsonElement("approvedAtUtc")]
        public DateTime? ApprovedAtUtc { get; set; }

        // === QR support ===
        // Server-generated token and checksum that the Operator scans.
        // Format suggestion: "booking:<ObjectId>;ts:<ticks>" | HMAC-SHA256(base64url)
        [BsonElement("qrToken")]
        public string? QrToken { get; set; }

        [BsonElement("qrChecksum")]
        public string? QrChecksum { get; set; }

        // Optional: raw/legacy QR string if you previously stored something here.
        [BsonElement("qrCode")]
        public string? QrCode { get; set; }
    }
}
