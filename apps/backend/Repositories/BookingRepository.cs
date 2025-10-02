using EvCharge.Api.Domain;
using MongoDB.Driver;

namespace EvCharge.Api.Repositories
{
    public interface IBookingRepository
    {
        Task EnsureIndexesAsync(CancellationToken ct);
        Task<string> CreateAsync(Booking b, CancellationToken ct);
        Task<Booking?> GetByIdAsync(string id, CancellationToken ct);
        Task<bool> ReplaceAsync(Booking b, CancellationToken ct);

        Task<List<Booking>> GetMineAsync(string ownerNic, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct);
        Task<List<Booking>> AdminListAsync(string? stationId, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct);

        // inventory
        Task EnsureInventoryDocAsync(string stationId, DateTime slotStartUtc, DateTime slotEndUtc, int capacity, CancellationToken ct);
        Task<bool> TryReserveAsync(string stationId, DateTime slotStartUtc, CancellationToken ct);
        Task ReleaseAsync(string stationId, DateTime slotStartUtc, CancellationToken ct);
    }

    public class BookingRepository : IBookingRepository
    {
        private readonly IMongoCollection<Booking> _bookings;
        private readonly IMongoCollection<StationSlotInventory> _inv;

        public BookingRepository(IMongoDatabase db)
        {
            _bookings = db.GetCollection<Booking>("bookings");
            _inv = db.GetCollection<StationSlotInventory>("station_slot_inventory");
        }

        public async Task EnsureIndexesAsync(CancellationToken ct)
        {
            try
            {
                await _bookings.Indexes.CreateManyAsync(new[]
                {
                    new CreateIndexModel<Booking>(Builders<Booking>.IndexKeys.Ascending(x => x.OwnerNic).Descending(x => x.SlotStartUtc), new CreateIndexOptions { Name="ix_owner_time" }),
                    new CreateIndexModel<Booking>(Builders<Booking>.IndexKeys.Ascending(x => x.StationId).Ascending(x => x.SlotStartUtc).Ascending(x => x.Status), new CreateIndexOptions { Name="ix_station_time_status" }),
                    new CreateIndexModel<Booking>(Builders<Booking>.IndexKeys.Ascending(x => x.BookingCode), new CreateIndexOptions { Name="ix_code" })
                }, cancellationToken: ct);

                await _inv.Indexes.CreateOneAsync(
                    new CreateIndexModel<StationSlotInventory>(
                        Builders<StationSlotInventory>.IndexKeys.Ascending(x => x.StationId).Ascending(x => x.SlotStartUtc),
                        new CreateIndexOptions { Name = "ux_station_slot", Unique = true }),
                    cancellationToken: ct
                );
            }
            catch { /* idempotent */ }
        }

        public async Task<string> CreateAsync(Booking b, CancellationToken ct)
        {
            await _bookings.InsertOneAsync(b, cancellationToken: ct);
            return b.Id!;
        }

        public async Task<Booking?> GetByIdAsync(string id, CancellationToken ct)
            => await _bookings.Find(x => x.Id == id).FirstOrDefaultAsync(ct);

        public async Task<bool> ReplaceAsync(Booking b, CancellationToken ct)
        {
            var res = await _bookings.ReplaceOneAsync(x => x.Id == b.Id, b, cancellationToken: ct);
            return res.ModifiedCount > 0;
        }

        public async Task<List<Booking>> GetMineAsync(string ownerNic, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
        {
            var fb = Builders<Booking>.Filter;
            var f = fb.Eq(x => x.OwnerNic, ownerNic);
            if (!string.IsNullOrWhiteSpace(status)) f &= fb.Eq(x => x.Status, status);
            if (fromUtc.HasValue) f &= fb.Gte(x => x.SlotStartUtc, fromUtc.Value);
            if (toUtc.HasValue) f &= fb.Lte(x => x.SlotStartUtc, toUtc.Value);
            return await _bookings.Find(f).SortByDescending(x => x.SlotStartUtc).Limit(200).ToListAsync(ct);
        }

        public async Task<List<Booking>> AdminListAsync(string? stationId, string? status, DateTime? fromUtc, DateTime? toUtc, CancellationToken ct)
        {
            var fb = Builders<Booking>.Filter;
            var f = fb.Empty;
            if (!string.IsNullOrWhiteSpace(stationId)) f &= fb.Eq(x => x.StationId, stationId);
            if (!string.IsNullOrWhiteSpace(status)) f &= fb.Eq(x => x.Status, status);
            if (fromUtc.HasValue) f &= fb.Gte(x => x.SlotStartUtc, fromUtc.Value);
            if (toUtc.HasValue) f &= fb.Lte(x => x.SlotStartUtc, toUtc.Value);
            return await _bookings.Find(f).SortBy(x => x.SlotStartUtc).Limit(500).ToListAsync(ct);
        }

        public async Task EnsureInventoryDocAsync(string stationId, DateTime slotStartUtc, DateTime slotEndUtc, int capacity, CancellationToken ct)
        {
            var up = Builders<StationSlotInventory>.Update
                .SetOnInsert(x => x.StationId, stationId)
                .SetOnInsert(x => x.SlotStartUtc, slotStartUtc)
                .SetOnInsert(x => x.SlotEndUtc, slotEndUtc)
                .SetOnInsert(x => x.Capacity, capacity)
                .SetOnInsert(x => x.Reserved, 0)
                .Set(x => x.UpdatedAtUtc, DateTime.UtcNow);

            await _inv.UpdateOneAsync(
                x => x.StationId == stationId && x.SlotStartUtc == slotStartUtc,
                up,
                new UpdateOptions { IsUpsert = true },
                ct
            );
        }

        public async Task<bool> TryReserveAsync(string stationId, DateTime slotStartUtc, CancellationToken ct)
        {
            var up = Builders<StationSlotInventory>.Update.Inc(x => x.Reserved, 1).Set(x => x.UpdatedAtUtc, DateTime.UtcNow);
            var res = await _inv.UpdateOneAsync(
                x => x.StationId == stationId && x.SlotStartUtc == slotStartUtc && x.Reserved < x.Capacity,
                up,
                cancellationToken: ct
            );
            return res.ModifiedCount == 1;
        }

        public async Task ReleaseAsync(string stationId, DateTime slotStartUtc, CancellationToken ct)
        {
            var up = Builders<StationSlotInventory>.Update
                .Inc(x => x.Reserved, -1)
                .Set(x => x.UpdatedAtUtc, DateTime.UtcNow);
            await _inv.UpdateOneAsync(
                x => x.StationId == stationId && x.SlotStartUtc == slotStartUtc && x.Reserved > 0,
                up,
                cancellationToken: ct
            );
        }
    }
}
