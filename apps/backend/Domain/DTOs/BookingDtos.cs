namespace EvCharge.Api.Domain.DTOs
{
    public class BookingCreateRequest
    {
        public string StationId { get; set; } = string.Empty;
        public string LocalDate { get; set; } = ""; // yyyy-MM-dd (station local)
        public string StartTime { get; set; } = ""; // HH:mm (station local)
        public int Minutes { get; set; } = 60;      // must equal station.DefaultSlotMinutes (phase-1)
        public string? Notes { get; set; }
    }

    public class BookingUpdateRequest
    {
        public string LocalDate { get; set; } = "";
        public string StartTime { get; set; } = "";
        public int Minutes { get; set; } = 60;
        public string? Notes { get; set; }
    }

    public class BookingResponse
    {
        public string Id { get; set; } = string.Empty;
        public string BookingCode { get; set; } = string.Empty;
        public string OwnerNic { get; set; } = string.Empty;
        public string StationId { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public string SlotStartLocal { get; set; } = "";
        public DateTime SlotStartUtc { get; set; }
        public DateTime SlotEndUtc { get; set; }
        public int SlotMinutes { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public DateTime? QrExpiresAtUtc { get; set; }
    }

    public class BookingApprovalResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Status { get; set; } = "Approved";
        public string QrToken { get; set; } = string.Empty;  // returned once
        public DateTime QrExpiresAtUtc { get; set; }
    }

    public class BookingListItem
    {
        public string Id { get; set; } = string.Empty;
        public string BookingCode { get; set; } = string.Empty;
        public string StationId { get; set; } = string.Empty;
        public string Status { get; set; } = "Pending";
        public DateTime SlotStartUtc { get; set; }
        public int SlotMinutes { get; set; }
    }
}
