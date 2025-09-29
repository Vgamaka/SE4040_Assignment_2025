using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace backend.Models
{
    /// <summary>
    /// Represents a charging slot booking made by an EV Owner.
    /// </summary>
    public class Booking
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }   // ✅ Nullable → MongoDB generates this

        [BsonElement("ownerNIC")]
        public string? OwnerNIC { get; set; }  // FK → EvOwner NIC

        [BsonElement("stationId")]
        public string? StationId { get; set; } // FK → ChargingStation Id

        [BsonElement("reservationDateTime")]
        public DateTime ReservationDateTime { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("status")]
        public string Status { get; set; } = "Pending";
        // Pending, Approved, Cancelled, Completed

        [BsonElement("qrCode")]
        public string? QrCode { get; set; }  // Optional: generated after approval
    }
}
