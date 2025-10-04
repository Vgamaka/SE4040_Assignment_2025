using System;

namespace EvCharge.Api.Domain.DTOs
{
    public class QrVerifyRequest
    {
        public string QrToken { get; set; } = string.Empty;
    }

    public class QrVerifyResponse
    {
        public bool Valid { get; set; }
        public string? BookingId { get; set; }
        public string? StationId { get; set; }
        public DateTime? ExpUtc { get; set; }
        public string? Status { get; set; }
        public string? Message { get; set; }
    }

    public class SessionCheckInRequest
    {
        // If provided, we cross-check against the token's booking
        public string? BookingId { get; set; }
        public string QrToken { get; set; } = string.Empty;
    }

    public class SessionFinalizeRequest
    {
        public string BookingId { get; set; } = string.Empty;
        public decimal EnergyKwh { get; set; }
        public decimal UnitPrice { get; set; }
        public string? Notes { get; set; }
    }

    public class SessionReceiptResponse
    {
        public string BookingId { get; set; } = string.Empty;
        public string BookingCode { get; set; } = string.Empty;
        public string StationId { get; set; } = string.Empty;
        public DateTime SlotStartUtc { get; set; }
        public int SlotMinutes { get; set; }
        public decimal EnergyKwh { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Total { get; set; }
        public DateTime CompletedAtUtc { get; set; }
    }
}

