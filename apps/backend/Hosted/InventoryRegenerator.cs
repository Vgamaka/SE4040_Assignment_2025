using EvCharge.Api.Domain;
using EvCharge.Api.Options;
using EvCharge.Api.Repositories;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EvCharge.Api.Services;

namespace EvCharge.Api.Hosted
{
    public class InventoryRegenerator : BackgroundService
    {
        private readonly ILogger<InventoryRegenerator> _log;
        private readonly IStationRepository _stations;
        private readonly IInventoryService _inventory;
        private readonly InventoryOptions _opts;

        public InventoryRegenerator(
            ILogger<InventoryRegenerator> log,
            IStationRepository stations,
            IInventoryService inventory,
            IOptions<InventoryOptions> opts)
        {
            _log = log;
            _stations = stations;
            _inventory = inventory;
            _opts = opts.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _log.LogInformation("InventoryRegenerator started. HorizonDays={H}, Interval={M}m",
                _opts.HorizonDays, _opts.RegenIntervalMinutes);

            // First immediate run
            await RegenerateAll(stoppingToken);

            // Periodic timer
            var interval = TimeSpan.FromMinutes(Math.Max(15, _opts.RegenIntervalMinutes));
            using var timer = new PeriodicTimer(interval);

            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await RegenerateAll(stoppingToken);
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Inventory regeneration loop error.");
                }
            }
        }

        private async Task RegenerateAll(CancellationToken ct)
        {
            const int pageSize = 200;
            int page = 1;

            while (!ct.IsCancellationRequested)
            {
                var (items, total) = await _stations.ListAsync(
                    type: null, status: "Active", minConnectors: null,
                    page: page, pageSize: pageSize, ct);

                if (items.Count == 0) break;

                foreach (var st in items)
                {
                    var sch = await _stations.GetScheduleAsync(st.Id!, ct);

                    // Start from station's current local date
                    var tzId = string.IsNullOrWhiteSpace(st.HoursTimezone) ? "UTC" : st.HoursTimezone;
                    TimeZoneInfo tz;
                    try { tz = TimeZoneInfo.FindSystemTimeZoneById(tzId); } catch { tz = TimeZoneInfo.FindSystemTimeZoneById("UTC"); }
                    var localToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTime.UtcNow, tz).Date);

                    var upserts = await _inventory.RegenerateForStationAsync(st, sch, localToday, _opts.HorizonDays, ct);
                    if (upserts > 0)
                        _log.LogInformation("Inventory regen: station={Station} upserts={Count}", st.Id, upserts);
                }

                // next page
                page++;
                if (page > Math.Max(1, (int)Math.Ceiling(total / (double)pageSize))) break;
            }
        }
    }
}
