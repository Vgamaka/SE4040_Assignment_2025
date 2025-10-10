using System;
using System.Collections.Generic;

namespace EvCharge.Api.Domain.DTOs
{
    // ---- Requests ----

    public class SummaryReportRequest
    {
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
        public string? StationId { get; set; }
    }

    public class TimeSeriesRequest
    {
        // bookings: created|approved|rejected|cancelled|checkedin|completed
        // revenue:  metric is ignored (always "revenue")
        public string Metric { get; set; } = "created";
        public string? StationId { get; set; }
        public DateTime FromUtc { get; set; }
        public DateTime ToUtc { get; set; }
        // day|week|month
        public string Granularity { get; set; } = "day";
    }

    // ---- Responses ----

    public class SummaryReportResponse
    {
        public long BookingsCreated { get; set; }
        public long Approved { get; set; }
        public long Rejected { get; set; }
        public long Cancelled { get; set; }
        public long CheckedIn { get; set; }
        public long Completed { get; set; }
        public double ApprovalRate { get; set; }
        public double CheckInRate { get; set; }
        public double CompletionRate { get; set; }
        public decimal RevenueTotal { get; set; }
        public decimal EnergyTotalKwh { get; set; }
    }

    public class TimeSeriesPoint
    {
        public DateTime BucketStartUtc { get; set; }
        public decimal Value { get; set; }
    }

    public class TimeSeriesResponse
    {
        public string Metric { get; set; } = "";
        public string Granularity { get; set; } = "day";
        public List<TimeSeriesPoint> Points { get; set; } = new();
    }

    public class StationUtilizationPoint
    {
        public string Date { get; set; } = ""; // yyyy-MM-dd (station local)
        public double UtilizationPct { get; set; } // Reserved / Capacity
        public int Reserved { get; set; }
        public int Capacity { get; set; }
    }

    public class StationUtilizationResponse
    {
        public string StationId { get; set; } = "";
        public List<StationUtilizationPoint> Daily { get; set; } = new();
    }

    public class RevenueByStationItem
    {
        public string StationId { get; set; } = "";
        public decimal Revenue { get; set; }
    }

    public class RevenueByStationResponse
    {
        public List<RevenueByStationItem> Items { get; set; } = new();
        public decimal Total { get; set; }
    }

    public class OccupancyHeatCell
    {
        // Day of week: 0=Sunday,...,6=Saturday (station local)
        public int Dow { get; set; }
        public int Hour { get; set; } // 0..23 (station local)
        public double AvgReservedPct { get; set; }
    }

    public class OccupancyHeatmapResponse
    {
        public string StationId { get; set; } = "";
        public List<OccupancyHeatCell> Cells { get; set; } = new();
    }
}
