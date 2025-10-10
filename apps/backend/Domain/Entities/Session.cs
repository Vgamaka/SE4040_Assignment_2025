using System;

namespace EvCharge.Api.Domain.Entities
{
    public class Session
    {
        public string Id { get; set; } = default!;
        public string BookingId { get; set; } = default!;
        public string StationId { get; set; } = default!;
        public string OwnerNIC { get; set; } = default!;

        public DateTime CreatedAtUtc { get; set; }
        public DateTime? CheckInUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }

        public decimal? EnergyKwh { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? Total { get; set; }

        public string? Notes { get; set; }
        public string Status { get; set; } = "CheckedIn"; // initial when created at check-in
    }
}
