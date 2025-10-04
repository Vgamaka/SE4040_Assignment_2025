using EvCharge.Api.Domain;
using EvCharge.Api.Options;
using EvCharge.Api.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Microsoft.Extensions.DependencyInjection;

namespace EvCharge.Api.Hosted
{
    public class NoShowSweeper : BackgroundService
    {
        private readonly ILogger<NoShowSweeper> _log;
        private readonly IPolicyService _policy;
        private readonly IInventoryService _inventory;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly PolicyOptions _opts;
        private readonly IMongoCollection<Booking> _bookings;

        public NoShowSweeper(
            ILogger<NoShowSweeper> log,
            IOptions<PolicyOptions> opts,
            IMongoDatabase db,
            IPolicyService policy,
            IInventoryService inventory,
            IServiceScopeFactory scopeFactory)
                {
            _log = log;
            _policy = policy;
            _inventory = inventory;
            _scopeFactory = scopeFactory;
            _opts = opts.Value;
            _bookings = db.GetCollection<Booking>("bookings");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_opts.EnableNoShowSweeper)
            {
                _log.LogInformation("NoShowSweeper disabled via PolicyOptions.");
                return;
            }

            var interval = TimeSpan.FromMinutes(Math.Max(1, _opts.NoShowSweepIntervalMinutes));
            _log.LogInformation("NoShowSweeper running every {m} minutes (grace={g}m).",
                _opts.NoShowSweepIntervalMinutes, _opts.LatestCheckInGraceMinutes);

            using var timer = new PeriodicTimer(interval);
            do
            {
                try { await SweepOnce(stoppingToken); }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    _log.LogError(ex, "NoShowSweeper error");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task SweepOnce(CancellationToken ct)
        {
            var now = DateTime.UtcNow;

            // Fetch candidates: Approved that already started (limit window to reduce load).
            // We’ll do precise eligibility via policy in-memory.
            var fb = Builders<Booking>.Filter;
            var started = fb.And(
                fb.Eq(b => b.Status, "Approved"),
                fb.Lte(b => b.SlotStartUtc, now)
            );

            var list = await _bookings.Find(started).Limit(1000).ToListAsync(ct);
            int changed = 0;

            foreach (var b in list)
            {
                if (!_policy.IsNoShowEligible(b, now)) continue;

                // Mark NoShow
                b.Status = "NoShow";
                b.UpdatedAtUtc = now;
                b.UpdatedBy = "system-noshow";

                var res = await _bookings.ReplaceOneAsync(x => x.Id == b.Id, b, cancellationToken: ct);
                if (res.ModifiedCount == 0) continue;
                changed++;

                // Release reserved capacity (best-effort)
                try { await _inventory.ReleaseAsync(b.StationId, b.SlotStartUtc, ct); } catch { /* best-effort */ }

                // Audit + Notify (best-effort) via scoped services
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var audit  = scope.ServiceProvider.GetRequiredService<IAuditService>();
                    var notify = scope.ServiceProvider.GetRequiredService<INotificationService>();

                    await audit.LogAsync("booking", b.Id!, "NoShow", "system-noshow", "System",
                        new Dictionary<string, object?> { ["stationId"] = b.StationId }, ct);

                    await notify.EnqueueAsync("NoShow", b.OwnerNic,
                        "Booking marked No-Show",
                        $"Booking {b.BookingCode} was marked as No-Show (missed check-in window).",
                        new Dictionary<string, object?> { ["bookingId"] = b.Id, ["bookingCode"] = b.BookingCode }, ct);
                }
                catch { /* best-effort */ }
            }

            if (changed > 0)
                _log.LogInformation("NoShowSweeper: marked {count} booking(s) as NoShow.", changed);
        }
    }
}
