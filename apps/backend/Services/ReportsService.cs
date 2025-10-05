using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EvCharge.Api.Domain;
using EvCharge.Api.Domain.DTOs;
using EvCharge.Api.Domain.Entities;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace EvCharge.Api.Services
{
    public interface IReportsService
    {
        Task<SummaryReportResponse> GetSummaryAsync(DateTime? fromUtc, DateTime? toUtc, string? stationId, CancellationToken ct);

        /// <summary>
        /// metric: created|approved|rejected|cancelled|checkedin|completed
        /// granularity: day|week|month (UTC buckets)
        /// </summary>
        Task<TimeSeriesResponse> GetBookingTimeSeriesAsync(string metric, string? stationId, DateTime fromUtc, DateTime toUtc, string granularity, CancellationToken ct);

        /// <summary>
        /// Sum revenue (Session.Total) grouped by UTC buckets (day|week|month).
        /// </summary>
        Task<TimeSeriesResponse> GetRevenueTimeSeriesAsync(string? stationId, DateTime fromUtc, DateTime toUtc, string granularity, CancellationToken ct);

        /// <summary>
        /// Daily utilization for a station over local dates [fromLocalDate, toLocalDate] inclusive.
        /// </summary>
        Task<StationUtilizationResponse> GetStationUtilizationAsync(string stationId, DateOnly fromLocalDate, DateOnly toLocalDate, CancellationToken ct);

        /// <summary>
        /// Sum revenue per station within [fromUtc, toUtc].
        /// </summary>
        Task<RevenueByStationResponse> GetRevenueByStationAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct);

        /// <summary>
        /// Heatmap: average Reserved/Capacity by (DayOfWeek, Hour) for a station.
        /// </summary>
        Task<OccupancyHeatmapResponse> GetOccupancyHeatmapAsync(string stationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct);
    }

    public class ReportsService : IReportsService
    {
        private readonly ILogger<ReportsService> _log;
        private readonly IMongoCollection<Booking> _bookings;
        private readonly IMongoCollection<Session> _sessions;
        private readonly IMongoCollection<StationSlotInventory> _inv;
        private readonly IMongoCollection<Station> _stations;

        public ReportsService(ILogger<ReportsService> log, IMongoDatabase db)
        {
            _log = log;
            _bookings = db.GetCollection<Booking>("bookings");
            _sessions = db.GetCollection<Session>("sessions");
            _inv = db.GetCollection<StationSlotInventory>("station_slot_inventory");
            _stations = db.GetCollection<Station>("stations");
            EnsureIndexes();
        }

        private void EnsureIndexes()
        {
            try
            {
                _sessions.Indexes.CreateMany(new[]
                {
                    new CreateIndexModel<Session>(
                        Builders<Session>.IndexKeys.Ascending(x => x.StationId).Descending(x => x.CompletedAtUtc),
                        new CreateIndexOptions { Name = "ix_station_completed_desc" }),
                    new CreateIndexModel<Session>(
                        Builders<Session>.IndexKeys.Ascending(x => x.CompletedAtUtc),
                        new CreateIndexOptions { Name = "ix_session_completed" })
                });
            }
            catch { /* best-effort/idempotent */ }
        }

        public async Task<SummaryReportResponse> GetSummaryAsync(DateTime? fromUtc, DateTime? toUtc, string? stationId, CancellationToken ct)
        {
            var from = fromUtc ?? DateTime.UtcNow.AddDays(-30);
            var to = toUtc ?? DateTime.UtcNow;

            // ---- Bookings (created/approved/rejected/cancelled) ----
            var fb = Builders<Booking>.Filter;
            var common = fb.Gte(x => x.SlotStartUtc, from) & fb.Lte(x => x.SlotStartUtc, to);
            if (!string.IsNullOrWhiteSpace(stationId))
                common &= fb.Eq(x => x.StationId, stationId);

            var createdFilter = fb.Gte(x => x.CreatedAtUtc, from) & fb.Lte(x => x.CreatedAtUtc, to);
            if (!string.IsNullOrWhiteSpace(stationId))
                createdFilter &= fb.Eq(x => x.StationId, stationId);

            var created = await _bookings.CountDocumentsAsync(createdFilter, cancellationToken: ct);
            var approved = await _bookings.CountDocumentsAsync(common & fb.Eq(x => x.Status, "Approved"), cancellationToken: ct);
            var rejected = await _bookings.CountDocumentsAsync(common & fb.Eq(x => x.Status, "Rejected"), cancellationToken: ct);
            var cancelled = await _bookings.CountDocumentsAsync(common & fb.Eq(x => x.Status, "Cancelled"), cancellationToken: ct);
            var checkedIn = await _bookings.CountDocumentsAsync(common & fb.Eq(x => x.Status, "CheckedIn"), cancellationToken: ct);
            var completedBookings = await _bookings.CountDocumentsAsync(common & fb.Eq(x => x.Status, "Completed"), cancellationToken: ct);


            // ---- Sessions (revenue + energy, completed window) ----
            var fs = Builders<Session>.Filter.Gte(s => s.CompletedAtUtc, from) & Builders<Session>.Filter.Lte(s => s.CompletedAtUtc, to);
            if (!string.IsNullOrWhiteSpace(stationId))
                fs &= Builders<Session>.Filter.Eq(s => s.StationId, stationId);

            var sessions = await _sessions.Find(fs).Project(s => new { s.Total, s.EnergyKwh }).ToListAsync(ct);
            var revenue = sessions.Sum(s => s.Total ?? 0m);
            var energy = sessions.Sum(s => s.EnergyKwh ?? 0m);

            // ---- Rates ----
            double approvalRate = created > 0 ? (double)approved / created : 0;
            double checkInRate = approved > 0 ? (double)checkedIn / approved : 0;
            double completionRate = checkedIn > 0 ? (double)completedBookings / checkedIn : 0;

            return new SummaryReportResponse
            {
                BookingsCreated = created,
                Approved = approved,
                Rejected = rejected,
                Cancelled = cancelled,
                CheckedIn = checkedIn,
                Completed = completedBookings,
                ApprovalRate = Math.Round(approvalRate, 4),
                CheckInRate = Math.Round(checkInRate, 4),
                CompletionRate = Math.Round(completionRate, 4),
                RevenueTotal = revenue,
                EnergyTotalKwh = energy
            };
        }

        public async Task<TimeSeriesResponse> GetBookingTimeSeriesAsync(string metric, string? stationId, DateTime fromUtc, DateTime toUtc, string granularity, CancellationToken ct)
        {
            metric = (metric ?? "created").Trim().ToLowerInvariant();
            granularity = NormalizeGranularity(granularity);

            var points = new Dictionary<DateTime, decimal>();

            bool IsSessionMetric(string m) => m is "checkedin" or "completed";
            var stationFilterSession = Builders<Session>.Filter.Empty;
            var stationFilterBooking = Builders<Booking>.Filter.Empty;

            if (!string.IsNullOrWhiteSpace(stationId))
            {
                stationFilterSession = Builders<Session>.Filter.Eq(s => s.StationId, stationId);
                stationFilterBooking = Builders<Booking>.Filter.Eq(b => b.StationId, stationId);
            }

            if (metric == "created")
            {
                var f = Builders<Booking>.Filter.Gte(b => b.CreatedAtUtc, fromUtc) & Builders<Booking>.Filter.Lte(b => b.CreatedAtUtc, toUtc) & stationFilterBooking;
                var proj = await _bookings.Find(f).Project(b => b.CreatedAtUtc).ToListAsync(ct);
                foreach (var dt in proj) AddToBucket(points, dt, granularity, 1);
            }
            else if (metric == "approved" || metric == "rejected" || metric == "cancelled")
            {
                // Use status timestamps if available; fall back to UpdatedAtUtc when needed
                var fBase = Builders<Booking>.Filter.Gte(b => b.SlotStartUtc, fromUtc) & Builders<Booking>.Filter.Lte(b => b.SlotStartUtc, toUtc) & stationFilterBooking;
                var status = metric switch
                {
                    "approved" => "Approved",
                    "rejected" => "Rejected",
                    "cancelled" => "Cancelled",
                    _ => ""
                };
                var f = fBase & Builders<Booking>.Filter.Eq(b => b.Status, status);

                var times = await _bookings.Find(f).Project(b =>
                    status == "Approved" ? (b.ApprovedAtUtc ?? b.UpdatedAtUtc ?? b.SlotStartUtc) :
                    status == "Rejected" ? (b.RejectedAtUtc ?? b.UpdatedAtUtc ?? b.SlotStartUtc) :
                    (b.CancelledAtUtc ?? b.UpdatedAtUtc ?? b.SlotStartUtc) // Cancelled
                ).ToListAsync(ct);

                foreach (var dt in times)
                    AddToBucket(points, dt, granularity, 1);
            }
            else if (IsSessionMetric(metric))
            {
                if (metric == "checkedin")
                {
                    var f = Builders<Session>.Filter.Gte(s => s.CheckInUtc, fromUtc) & Builders<Session>.Filter.Lte(s => s.CheckInUtc, toUtc) & stationFilterSession;
                    var times = await _sessions.Find(f).Project(s => s.CheckInUtc).ToListAsync(ct);
                    foreach (var dt in times.Where(x => x.HasValue).Select(x => x!.Value))
                        AddToBucket(points, dt, granularity, 1);
                }
                else // completed
                {
                    var f = Builders<Session>.Filter.Gte(s => s.CompletedAtUtc, fromUtc) & Builders<Session>.Filter.Lte(s => s.CompletedAtUtc, toUtc) & stationFilterSession;
                    var times = await _sessions.Find(f).Project(s => s.CompletedAtUtc).ToListAsync(ct);
                    foreach (var dt in times.Where(x => x.HasValue).Select(x => x!.Value))
                        AddToBucket(points, dt, granularity, 1);
                }
            }
            else
            {
                // Unknown metric -> empty series
            }

            var ordered = points.OrderBy(k => k.Key).Select(kv => new TimeSeriesPoint { BucketStartUtc = kv.Key, Value = kv.Value }).ToList();

            // Ensure buckets exist across full range (0-fill)
            ordered = FillGaps(ordered, fromUtc, toUtc, granularity);

            return new TimeSeriesResponse
            {
                Metric = metric,
                Granularity = granularity,
                Points = ordered
            };
        }

        public async Task<TimeSeriesResponse> GetRevenueTimeSeriesAsync(string? stationId, DateTime fromUtc, DateTime toUtc, string granularity, CancellationToken ct)
        {
            granularity = NormalizeGranularity(granularity);
            var points = new Dictionary<DateTime, decimal>();

            var f = Builders<Session>.Filter.Gte(s => s.CompletedAtUtc, fromUtc) & Builders<Session>.Filter.Lte(s => s.CompletedAtUtc, toUtc);
            if (!string.IsNullOrWhiteSpace(stationId))
                f &= Builders<Session>.Filter.Eq(s => s.StationId, stationId);

            var docs = await _sessions.Find(f).Project(s => new { s.CompletedAtUtc, s.Total }).ToListAsync(ct);
            foreach (var d in docs.Where(x => x.CompletedAtUtc.HasValue))
                AddToBucket(points, d.CompletedAtUtc!.Value, granularity, d.Total ?? 0m);

            var ordered = points.OrderBy(k => k.Key).Select(kv => new TimeSeriesPoint { BucketStartUtc = kv.Key, Value = kv.Value }).ToList();
            ordered = FillGaps(ordered, fromUtc, toUtc, granularity);

            return new TimeSeriesResponse
            {
                Metric = "revenue",
                Granularity = granularity,
                Points = ordered
            };
        }

        public async Task<StationUtilizationResponse> GetStationUtilizationAsync(string stationId, DateOnly fromLocalDate, DateOnly toLocalDate, CancellationToken ct)
        {
            var st = await _stations.Find(x => x.Id == stationId).FirstOrDefaultAsync(ct) ?? throw new Exception("Station not found.");
            var tz = ResolveTz(st.HoursTimezone);

            // UTC window for query
            var startLocal = DateTime.SpecifyKind(fromLocalDate.ToDateTime(new TimeOnly(0, 0)), DateTimeKind.Unspecified);
            var endLocalExclusive = DateTime.SpecifyKind(toLocalDate.AddDays(1).ToDateTime(new TimeOnly(0, 0)), DateTimeKind.Unspecified);

            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
            var toUtc = TimeZoneInfo.ConvertTimeToUtc(endLocalExclusive, tz);

            var f = Builders<StationSlotInventory>.Filter.And(
                Builders<StationSlotInventory>.Filter.Eq(x => x.StationId, stationId),
                Builders<StationSlotInventory>.Filter.Gte(x => x.SlotStartUtc, fromUtc),
                Builders<StationSlotInventory>.Filter.Lt(x => x.SlotStartUtc, toUtc)
            );

            var docs = await _inv.Find(f).Project(x => new { x.SlotStartUtc, x.Reserved, x.Capacity }).ToListAsync(ct);

            var byLocalDate = new Dictionary<string, (int reserved, int capacity)>();
            foreach (var d in docs)
            {
                var local = TimeZoneInfo.ConvertTimeFromUtc(d.SlotStartUtc, tz);
                var key = local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (!byLocalDate.ContainsKey(key)) byLocalDate[key] = (0, 0);
                var cur = byLocalDate[key];
                byLocalDate[key] = (cur.reserved + d.Reserved, cur.capacity + d.Capacity);
            }

            var result = new List<StationUtilizationPoint>();
            for (var cursor = fromLocalDate; cursor <= toLocalDate; cursor = cursor.AddDays(1))
            {
                var k = cursor.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                byLocalDate.TryGetValue(k, out var agg);
                var pct = agg.capacity > 0 ? (double)agg.reserved / agg.capacity : 0d;

                result.Add(new StationUtilizationPoint
                {
                    Date = k,
                    Reserved = agg.reserved,
                    Capacity = agg.capacity,
                    UtilizationPct = Math.Round(pct, 4)
                });
            }

            return new StationUtilizationResponse
            {
                StationId = stationId,
                Daily = result
            };
        }

        public async Task<RevenueByStationResponse> GetRevenueByStationAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            var f = Builders<Session>.Filter.Gte(s => s.CompletedAtUtc, fromUtc) & Builders<Session>.Filter.Lte(s => s.CompletedAtUtc, toUtc);
            var docs = await _sessions.Find(f).Project(s => new { s.StationId, s.Total }).ToListAsync(ct);

            var byStation = docs
                .GroupBy(x => x.StationId)
                .Select(g => new RevenueByStationItem
                {
                    StationId = g.Key,
                    Revenue = g.Sum(x => x.Total ?? 0m)
                })
                .OrderByDescending(x => x.Revenue)
                .ToList();

            return new RevenueByStationResponse
            {
                Items = byStation,
                Total = byStation.Sum(x => x.Revenue)
            };
        }

        public async Task<OccupancyHeatmapResponse> GetOccupancyHeatmapAsync(string stationId, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
        {
            var st = await _stations.Find(x => x.Id == stationId).FirstOrDefaultAsync(ct) ?? throw new Exception("Station not found.");
            var tz = ResolveTz(st.HoursTimezone);

            var f = Builders<StationSlotInventory>.Filter.And(
                Builders<StationSlotInventory>.Filter.Eq(x => x.StationId, stationId),
                Builders<StationSlotInventory>.Filter.Gte(x => x.SlotStartUtc, fromUtc),
                Builders<StationSlotInventory>.Filter.Lt(x => x.SlotStartUtc, toUtc)
            );

            var docs = await _inv.Find(f).Project(x => new { x.SlotStartUtc, x.Reserved, x.Capacity }).ToListAsync(ct);

            var buckets = new Dictionary<(int dow, int hour), (double sumPct, int count)>();
            foreach (var d in docs)
            {
                var local = TimeZoneInfo.ConvertTimeFromUtc(d.SlotStartUtc, tz);
                int dow = (int)local.DayOfWeek; // 0=Sunday
                int hour = local.Hour;

                var pct = d.Capacity > 0 ? (double)d.Reserved / d.Capacity : 0d;
                var key = (dow, hour);

                if (!buckets.ContainsKey(key)) buckets[key] = (0, 0);
                var cur = buckets[key];
                buckets[key] = (cur.sumPct + pct, cur.count + 1);
            }

            var cells = buckets
                .Select(kv => new OccupancyHeatCell
                {
                    Dow = kv.Key.dow,
                    Hour = kv.Key.hour,
                    AvgReservedPct = Math.Round(kv.Value.sumPct / Math.Max(1, kv.Value.count), 4)
                })
                .OrderBy(c => c.Dow).ThenBy(c => c.Hour)
                .ToList();

            return new OccupancyHeatmapResponse
            {
                StationId = stationId,
                Cells = cells
            };
        }

        // ---- helpers ----

        private static TimeZoneInfo ResolveTz(string tzId)
        {
            if (string.IsNullOrWhiteSpace(tzId)) tzId = "UTC";
            try { return TimeZoneInfo.FindSystemTimeZoneById(tzId); }
            catch { return TimeZoneInfo.FindSystemTimeZoneById("UTC"); }
        }

        private static string NormalizeGranularity(string? g)
        {
            var v = (g ?? "day").Trim().ToLowerInvariant();
            return v is "day" or "week" or "month" ? v : "day";
            // Buckets are computed in UTC.
        }

        private static DateTime TruncateToBucket(DateTime utc, string granularity)
        {
            if (utc.Kind != DateTimeKind.Utc) utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            return granularity switch
            {
                "day" => new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc),
                "week" => StartOfWeekUtc(utc, DayOfWeek.Monday),
                "month" => new DateTime(utc.Year, utc.Month, 1, 0, 0, 0, DateTimeKind.Utc),
                _ => new DateTime(utc.Year, utc.Month, utc.Day, 0, 0, 0, DateTimeKind.Utc)
            };
        }

        private static DateTime StartOfWeekUtc(DateTime utc, DayOfWeek start)
        {
            int diff = ((7 + (int)utc.DayOfWeek) - (int)start) % 7;
            var day = utc.Date.AddDays(-diff);
            return new DateTime(day.Year, day.Month, day.Day, 0, 0, 0, DateTimeKind.Utc);
        }

        private static void AddToBucket(IDictionary<DateTime, decimal> buckets, DateTime whenUtc, string granularity, decimal value)
        {
            var k = TruncateToBucket(whenUtc.ToUniversalTime(), granularity);
            if (!buckets.ContainsKey(k)) buckets[k] = 0m;
            buckets[k] += value;
        }

        private static List<TimeSeriesPoint> FillGaps(List<TimeSeriesPoint> ordered, DateTime fromUtc, DateTime toUtc, string granularity)
        {
            var map = ordered.ToDictionary(x => x.BucketStartUtc, x => x.Value);
            var cursor = TruncateToBucket(fromUtc.ToUniversalTime(), granularity);
            var end = TruncateToBucket(toUtc.ToUniversalTime(), granularity);

            var step = granularity switch
            {
                "day" => TimeSpan.FromDays(1),
                "week" => TimeSpan.FromDays(7),
                "month" => (TimeSpan?)null,
                _ => TimeSpan.FromDays(1)
            };

            var result = new List<TimeSeriesPoint>();
            if (granularity == "month")
            {
                while (cursor <= end)
                {
                    map.TryGetValue(cursor, out var v);
                    result.Add(new TimeSeriesPoint { BucketStartUtc = cursor, Value = v });
                    cursor = new DateTime(cursor.Year, cursor.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
                }
            }
            else
            {
                while (cursor <= end)
                {
                    map.TryGetValue(cursor, out var v);
                    result.Add(new TimeSeriesPoint { BucketStartUtc = cursor, Value = v });
                    cursor = cursor.Add(step!.Value);
                }
            }
            return result;
        }
    }
}
