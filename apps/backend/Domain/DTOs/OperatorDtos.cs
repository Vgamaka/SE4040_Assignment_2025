using System.ComponentModel.DataAnnotations;

namespace EvCharge.Api.Domain.DTOs
{
    // Inbox — one row per booking (Approved for operator's stations on a given date)
    public class OperatorInboxItem
    {
        public string Id { get; set; } = string.Empty;
        public string BookingCode { get; set; } = string.Empty;
        public string StationId { get; set; } = string.Empty;
        public DateTime SlotStartUtc { get; set; }
        public int SlotMinutes { get; set; }
        public string Status { get; set; } = "Approved";
        public string OwnerNicMasked { get; set; } = string.Empty; // e.g., "***-***-1234"
        public string SlotStartLocal { get; set; } = ""; // "yyyy-MM-ddTHH:mm" (as stored)
    }

    // POST /api/Operator/scan
    public class OperatorScanRequest
    {
        [Required] public string QrToken { get; set; } = string.Empty;
        // Optional: if scanner passes booking id it expects, we’ll cross-check with QR mapping
        public string? BookingId { get; set; }
    }

    // POST /api/Operator/exception
    public class OperatorExceptionRequest
    {
        [Required] public string BookingId { get; set; } = string.Empty;

        // Allowed: "NoShow" | "Aborted" | "CustomerCancelOnSite"
        [Required] public string Reason { get; set; } = string.Empty;

        public string? Notes { get; set; }
    }
}
