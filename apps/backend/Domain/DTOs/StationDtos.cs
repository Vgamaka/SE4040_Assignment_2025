namespace EvCharge.Api.Domain.DTOs
{
    public class StationCreateRequest
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "AC"; // AC|DC
        public int Connectors { get; set; } = 1;
        public bool AutoApproveEnabled { get; set; } = false;  // NEW
        public double Lat { get; set; }
        public double Lng { get; set; }
        public int DefaultSlotMinutes { get; set; } = 60; // 30|45|60|90|120
        public string HoursTimezone { get; set; } = "Asia/Colombo";
        public PricingDto Pricing { get; set; } = new();
    }

    public class StationUpdateRequest
    {
        public string? Name { get; set; }
        public string? Type { get; set; }
        public int? Connectors { get; set; }
        public bool? AutoApproveEnabled { get; set; }  // NEW
        public double? Lat { get; set; }
        public double? Lng { get; set; }
        public int? DefaultSlotMinutes { get; set; }
        public string? HoursTimezone { get; set; }
        public PricingDto? Pricing { get; set; }
    }

    public class PricingDto
    {
        public string Model { get; set; } = "flat"; // flat|hourly|kwh
        public decimal Base { get; set; } = 0;
        public decimal PerHour { get; set; } = 0;
        public decimal PerKwh { get; set; } = 0;
        public decimal TaxPct { get; set; } = 0;
    }

    public class StationResponse
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "AC";
        public int Connectors { get; set; }
        public string Status { get; set; } = "Active";
        public bool AutoApproveEnabled { get; set; }  // NEW
        public double Lat { get; set; }
        public double Lng { get; set; }
        public int DefaultSlotMinutes { get; set; }
        public string HoursTimezone { get; set; } = "Asia/Colombo";
        public PricingDto Pricing { get; set; } = new();
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }

    public class StationListItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "AC";
        public int Connectors { get; set; }
        public string Status { get; set; } = "Active";
        public bool AutoApproveEnabled { get; set; }  // NEW
        public double Lat { get; set; }
        public double Lng { get; set; }
        public PricingDto Pricing { get; set; } = new();
        public List<AvailabilitySummaryItem> AvailabilitySummary { get; set; } = new();
    }

    public class AvailabilitySummaryItem
    {
        public string Date { get; set; } = ""; // yyyy-MM-dd
        public int AvailableSlots { get; set; }
    }

    public class StationScheduleUpsertRequest
    {
        public WeeklyScheduleDto Weekly { get; set; } = new();
        public List<ScheduleExceptionDto> Exceptions { get; set; } = new();
        public List<CapacityOverrideDto> CapacityOverrides { get; set; } = new();
    }

    public class WeeklyScheduleDto
    {
        public List<DayTimeRangeDto> Mon { get; set; } = new();
        public List<DayTimeRangeDto> Tue { get; set; } = new();
        public List<DayTimeRangeDto> Wed { get; set; } = new();
        public List<DayTimeRangeDto> Thu { get; set; } = new();
        public List<DayTimeRangeDto> Fri { get; set; } = new();
        public List<DayTimeRangeDto> Sat { get; set; } = new();
        public List<DayTimeRangeDto> Sun { get; set; } = new();
    }

    public class DayTimeRangeDto
    {
        public string Start { get; set; } = "09:00"; // HH:mm
        public string End { get; set; } = "17:00";
    }

    public class ScheduleExceptionDto
    {
        public string Date { get; set; } = ""; // yyyy-MM-dd
        public bool Closed { get; set; } = false;
    }

    public class CapacityOverrideDto
    {
        public string Date { get; set; } = ""; // yyyy-MM-dd
        public int Connectors { get; set; } = 0;
    }

    public class StationScheduleResponse
    {
        public WeeklyScheduleDto Weekly { get; set; } = new();
        public List<ScheduleExceptionDto> Exceptions { get; set; } = new();
        public List<CapacityOverrideDto> CapacityOverrides { get; set; } = new();
        public DateTime UpdatedAtUtc { get; set; }
    }
}
