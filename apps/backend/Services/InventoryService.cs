using System.Globalization;
using EvCharge.Api.Domain;
using EvCharge.Api.Repositories;
using EvCharge.Api.Services;
using EvCharge.Api.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace EvCharge.Api.Services
{
    public interface IInventoryService
    {
        Task EnsureSlotAsync(string stationId, DateTime slotStartUtc, DateTime slotEndUtc, int capacity, CancellationToken ct);
        Task<bool> TryReserveAsync(string stationId, DateTime slotStartUtc, CancellationToken ct);
        Task ReleaseAsync(string stationId, DateTime slotStartUtc, CancellationToken ct);

        /// <summary>Generate / heal inventory for a station over [fromLocalDate, fromLocalDate+days).</summary>
        Task<int> RegenerateForStationAsync(Station station, StationSchedule? schedule, DateOnly fromLocalDate, int days, CancellationToken ct);
    }

    public class InventoryService : IInventoryService
    {
        private readonly ILogger<InventoryService> _log;
        private readonly IBookingRepository _bookingsRepo;
        private readonly IStationRepository _stationsRepo;
        private readonly IScheduleService _schedules;
        private readonly IMongoCollection<StationSlotInventory> _inv;
        private readonly InventoryOptions _opts;

        public InventoryService(
            ILogger<InventoryService> log,
            IBookingRepository bookingsRepo,
            IStationRepository stationsRepo,
            IScheduleService schedules,
            IMongoDatabase db,
            IOptions<InventoryOptions> opts)
        {
            _log = log;
            _bookingsRepo = bookingsRepo;
            _stationsRepo = stationsRepo;
            _schedules = schedules;
            _inv = db.GetCollection<StationSlotInventory>("station_slot_inventory");
            _opts = opts.Value;
        }

        public Task EnsureSlotAsync(string stationId, DateTime slotStartUtc, DateTime slotEndUtc, int capacity, CancellationToken ct)
            => _bookingsRepo.EnsureInventoryDocAsync(stationId, slotStartUtc, slotEndUtc, capacity, ct);

        public Task<bool> TryReserveAsync(string stationId, DateTime slotStartUtc, CancellationToken ct)
            => _bookingsRepo.TryReserveAsync(stationId, slotStartUtc, ct);

        public Task ReleaseAsync(string stationId, DateTime slotStartUtc, CancellationToken ct)
            => _bookingsRepo.ReleaseAsync(stationId, slotStartUtc, ct);

        public async Task<int> RegenerateForStationAsync(Station st, StationSchedule? sch, DateOnly fromLocalDate, int days, CancellationToken ct)
        {
            if (st.Status != "Active") return 0;

            var tz = ResolveTz(st.HoursTimezone);
            int upserts = 0;

            for (int d = 0; d < days; d++)
            {
                var localDate = fromLocalDate.AddDays(d);
                var localDateStr = localDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

                // Closed or zero capacity?
                var connectors = ResolveCapacityForDate(st, sch, localDateStr);
                if (connectors <= 0) continue;

                var ranges = GetRangesForDay(sch?.Weekly, localDate.DayOfWeek);
                if (ranges.Count == 0) continue;

                foreach (var r in ranges)
                {
                    if (!TimeSpan.TryParseExact(r.Start, "hh\\:mm", CultureInfo.InvariantCulture, out var start)) continue;
                    if (!TimeSpan.TryParseExact(r.End, "hh\\:mm", CultureInfo.InvariantCulture, out var end)) continue;
                    if (start >= end) continue;

                    var step = Math.Max(15, st.DefaultSlotMinutes); // guard minimal step
                    var cursor = start;

                    while (cursor + TimeSpan.FromMinutes(step) <= end)
                    {
                        var localStart = DateTime.SpecifyKind(localDate.ToDateTime(TimeOnly.FromTimeSpan(cursor)), DateTimeKind.Unspecified);
                        var localEnd = localStart.AddMinutes(step);

                        var startUtc = TimeZoneInfo.ConvertTimeToUtc(localStart, tz);
                        var endUtc = TimeZoneInfo.ConvertTimeToUtc(localEnd, tz);

                        await _bookingsRepo.EnsureInventoryDocAsync(st.Id!, startUtc, endUtc, connectors, ct);
                        upserts++;

                        cursor += TimeSpan.FromMinutes(step);
                    }
                }

                if (_opts.EnableHealing)
                {
                    // Best-effort: if capacity changed upwards, update docs; if downwards, keep at least Reserved.
                    await HealDayCapacitiesAsync(st.Id!, tz, localDate, connectors, ct);
                }
            }

            return upserts;
        }

        // ---- helpers ----

        private static TimeZoneInfo ResolveTz(string tzId)
        {
            if (string.IsNullOrWhiteSpace(tzId)) tzId = "UTC";
            try { return TimeZoneInfo.FindSystemTimeZoneById(tzId); }
            catch { return TimeZoneInfo.FindSystemTimeZoneById("UTC"); }
        }

        private static List<DayTimeRange> GetRangesForDay(WeeklySchedule? weekly, DayOfWeek dow)
        {
            if (weekly is null) return new();
            return dow switch
            {
                DayOfWeek.Monday    => weekly.Mon,
                DayOfWeek.Tuesday   => weekly.Tue,
                DayOfWeek.Wednesday => weekly.Wed,
                DayOfWeek.Thursday  => weekly.Thu,
                DayOfWeek.Friday    => weekly.Fri,
                DayOfWeek.Saturday  => weekly.Sat,
                DayOfWeek.Sunday    => weekly.Sun,
                _ => new()
            };
        }

        private static int ResolveCapacityForDate(Station st, StationSchedule? sch, string localDateStr)
        {
            var cap = st.Connectors;
            if (sch is null) return cap;

            if (sch.Exceptions.Any(e => e.Date == localDateStr && e.Closed)) return 0;
            var ov = sch.CapacityOverrides.FirstOrDefault(x => x.Date == localDateStr);
            if (ov is not null && ov.Connectors > 0) cap = ov.Connectors;
            return cap;
        }

        private async Task HealDayCapacitiesAsync(string stationId, TimeZoneInfo tz, DateOnly localDate, int targetCapacity, CancellationToken ct)
        {
            var startLocal = DateTime.SpecifyKind(localDate.ToDateTime(new TimeOnly(0, 0)), DateTimeKind.Unspecified);
            var endLocal = startLocal.AddDays(1);

            var fromUtc = TimeZoneInfo.ConvertTimeToUtc(startLocal, tz);
            var toUtc = TimeZoneInfo.ConvertTimeToUtc(endLocal, tz);

            // Update Capacity to at least targetCapacity; don't reduce below Reserved
            var filter = Builders<StationSlotInventory>.Filter.And(
                Builders<StationSlotInventory>.Filter.Eq(x => x.StationId, stationId),
                Builders<StationSlotInventory>.Filter.Gte(x => x.SlotStartUtc, fromUtc),
                Builders<StationSlotInventory>.Filter.Lt(x => x.SlotStartUtc, toUtc)
            );

            var docs = await _inv.Find(filter).ToListAsync(ct);
            foreach (var doc in docs)
            {
                var newCap = Math.Max(targetCapacity, doc.Reserved);
                if (newCap != doc.Capacity)
                {
                    var up = Builders<StationSlotInventory>.Update
                        .Set(x => x.Capacity, newCap)
                        .Set(x => x.UpdatedAtUtc, DateTime.UtcNow);
                    await _inv.UpdateOneAsync(x => x.Id == doc.Id, up, cancellationToken: ct);
                }
            }
        }
    }
}
