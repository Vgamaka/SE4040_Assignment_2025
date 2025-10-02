using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EvCharge.Api.Domain
{
    public class Booking
    {
        [BsonId, BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string BookingCode { get; set; } = string.Empty; // e.g., BK-7F3C
        public string OwnerNic { get; set; } = string.Empty;
        [BsonRepresentation(BsonType.ObjectId)]
        public string StationId { get; set; } = string.Empty;

        public DateTime SlotStartUtc { get; set; }
        public DateTime SlotEndUtc { get; set; }
        public string SlotStartLocal { get; set; } = ""; // yyyy-MM-ddTHH:mm (station local)
        public int SlotMinutes { get; set; }

        public string Status { get; set; } = "Pending"; // Pending|Approved|Rejected|Cancelled|Expired|Completed
        public string? Notes { get; set; }

        public string? QrTokenHash { get; set; }
        public DateTime? QrExpiresAtUtc { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public string? UpdatedBy { get; set; }

        public DateTime? ApprovedAtUtc { get; set; }
        public string? ApprovedBy { get; set; }
        public DateTime? RejectedAtUtc { get; set; }
        public string? RejectedBy { get; set; }
        public DateTime? CancelledAtUtc { get; set; }
        public string? CancelledBy { get; set; }
    }

    public class StationSlotInventory
    {
        [BsonId, BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string StationId { get; set; } = string.Empty;

        public DateTime SlotStartUtc { get; set; }
        public DateTime SlotEndUtc { get; set; }

        public int Capacity { get; set; } // connectors for that day (consider overrides)
        public int Reserved { get; set; } // count of bookings in {Pending, Approved}
        public DateTime UpdatedAtUtc { get; set; }
    }
}
