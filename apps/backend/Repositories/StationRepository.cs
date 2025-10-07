using System.Text.RegularExpressions;
using EvCharge.Api.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EvCharge.Api.Repositories
{
    public interface IStationRepository
    {
        Task<string> CreateAsync(Station s, CancellationToken ct);
        Task<Station?> GetByIdAsync(string id, CancellationToken ct);
        Task<bool> ReplaceAsync(Station s, CancellationToken ct);
        Task<(List<Station> items, long total)> ListAsync(string? type, string? status, int? minConnectors, int page, int pageSize, CancellationToken ct);
        Task<List<Station>> NearbyAsync(double lat, double lng, double radiusKm, string? type, CancellationToken ct);
        Task<(List<Station> items, long total)> AdminListAsync(string? type, string? status, int? minConnectors, string? backOfficeNic, string? q, int page, int pageSize, CancellationToken ct);

        // schedules
        Task<StationSchedule?> GetScheduleAsync(string stationId, CancellationToken ct);
        Task UpsertScheduleAsync(StationSchedule schedule, CancellationToken ct);

        // BackOffice scoping
        Task<(List<Station> items, long total)> ListByBackOfficeAsync(string backOfficeNic, int page, int pageSize, CancellationToken ct);
        Task<bool> BelongsToBackOfficeAsync(string stationId, string backOfficeNic, CancellationToken ct);
    }

    public class StationRepository : IStationRepository
    {
        private readonly IMongoCollection<Station> _stations;
        private readonly IMongoCollection<StationSchedule> _schedules;
        private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);

        public StationRepository(IMongoDatabase db)
        {
            _stations  = db.GetCollection<Station>("stations");
            _schedules = db.GetCollection<StationSchedule>("station_schedules");
            EnsureIndexes();
        }

        private void EnsureIndexes()
        {
            try
            {
                // Geo + common field indexes
                var geo = Builders<Station>.IndexKeys.Geo2DSphere("Location");
                _stations.Indexes.CreateOne(new CreateIndexModel<Station>(geo, new CreateIndexOptions { Name = "ix_Location_2dsphere" }));

                _stations.Indexes.CreateMany(new[]
                {
                    new CreateIndexModel<Station>(Builders<Station>.IndexKeys.Ascending(x => x.Status), new CreateIndexOptions{ Name="ix_status" }),
                    new CreateIndexModel<Station>(Builders<Station>.IndexKeys.Ascending(x => x.Type),   new CreateIndexOptions{ Name="ix_type" }),
                    new CreateIndexModel<Station>(
                        Builders<Station>.IndexKeys.Ascending(s => s.BackOfficeNic),
                        new CreateIndexOptions { Name = "ix_backOfficeNic" }
                    )
                });

                var scheduleUx = Builders<StationSchedule>.IndexKeys.Ascending(x => x.StationId);
                _schedules.Indexes.CreateOne(new CreateIndexModel<StationSchedule>(scheduleUx, new CreateIndexOptions { Name = "ux_stationId", Unique = true }));
            }
            catch { /* idempotent */ }
        }

        public async Task<string> CreateAsync(Station s, CancellationToken ct)
        {
            await _stations.InsertOneAsync(s, cancellationToken: ct);
            return s.Id!;
        }

        public async Task<Station?> GetByIdAsync(string id, CancellationToken ct)
            => await _stations.Find(x => x.Id == id).FirstOrDefaultAsync(ct);

        public async Task<bool> ReplaceAsync(Station s, CancellationToken ct)
        {
            var res = await _stations.ReplaceOneAsync(x => x.Id == s.Id, s, cancellationToken: ct);
            return res.ModifiedCount > 0;
        }

        public async Task<(List<Station> items, long total)> ListAsync(string? type, string? status, int? minConnectors, int page, int pageSize, CancellationToken ct)
        {
            var fb = Builders<Station>.Filter;
            var filter = fb.Empty;

            if (!string.IsNullOrWhiteSpace(type)) filter &= fb.Eq(x => x.Type, type);
            if (!string.IsNullOrWhiteSpace(status)) filter &= fb.Eq(x => x.Status, status);
            if (minConnectors.HasValue) filter &= fb.Gte(x => x.Connectors, minConnectors.Value);

            var find  = _stations.Find(filter);
            var total = await find.CountDocumentsAsync(ct);
            var items = await find.Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
            return (items, total);
        }

        public async Task<List<Station>> NearbyAsync(double lat, double lng, double radiusKm, string? type, CancellationToken ct)
        {
            var fb  = Builders<Station>.Filter;
            var geo = fb.NearSphere("Location", lng, lat, maxDistance: radiusKm * 1000);

            var filter = geo & fb.Eq(x => x.Status, "Active");
            if (!string.IsNullOrWhiteSpace(type)) filter &= fb.Eq(x => x.Type, type);

            return await _stations.Find(filter).Limit(100).ToListAsync(ct);
        }

        // schedules
        public async Task<StationSchedule?> GetScheduleAsync(string stationId, CancellationToken ct)
            => await _schedules.Find(x => x.StationId == stationId).FirstOrDefaultAsync(ct);

        public async Task UpsertScheduleAsync(StationSchedule schedule, CancellationToken ct)
            => await _schedules.ReplaceOneAsync(x => x.StationId == schedule.StationId, schedule, new ReplaceOptions { IsUpsert = true }, ct);

        // ========= BackOffice scoping helpers =========

        private static string NormalizeNic(string nic) => (nic ?? string.Empty).Trim();

        // Case-insensitive exact match for "backOfficeNic" (stored) and legacy "updatedBy"
        private static FilterDefinition<Station> BackOfficeOwnerFilter(string backOfficeNic)
        {
            var nic = (backOfficeNic ?? string.Empty).Trim();
            var fb  = Builders<Station>.Filter;
            // tolerate leading/trailing whitespace in stored values
            var rx  = new BsonRegularExpression("^\\s*" + Regex.Escape(nic) + "\\s*$", "i");

            // typed eq (fast path) OR regex on raw field (case-insensitive, whitespace tolerant)
            var own = fb.Or(fb.Eq(s => s.BackOfficeNic, nic), fb.Regex("backOfficeNic", rx));
            var upd = fb.Or(fb.Eq(s => s.UpdatedBy,    nic), fb.Regex("updatedBy",    rx)); // legacy
            return own | upd;
        }

        public async Task<(List<Station> items, long total)> ListByBackOfficeAsync(
            string backOfficeNic, int page, int pageSize, CancellationToken ct)
        {
            var filter = BackOfficeOwnerFilter(backOfficeNic);

            var find  = _stations.Find(filter, new FindOptions { Collation = CaseInsensitive });
            var total = await _stations.CountDocumentsAsync(filter, new CountOptions { Collation = CaseInsensitive }, ct);
            var items = await find.Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
            return (items, total);
        }

        public async Task<bool> BelongsToBackOfficeAsync(string stationId, string backOfficeNic, CancellationToken ct)
        {
            var idFilter = Builders<Station>.Filter.Eq(x => x.Id, stationId);
            var owner    = BackOfficeOwnerFilter(backOfficeNic);
            var filter   = idFilter & owner;

            var find = _stations.Find(filter, new FindOptions { Collation = CaseInsensitive });
            return await find.AnyAsync(ct);
        }

        public async Task<(List<Station> items, long total)> AdminListAsync(
    string? type, string? status, int? minConnectors, string? backOfficeNic, string? q,
    int page, int pageSize, CancellationToken ct)
{
    var fb = Builders<Station>.Filter;
    var filter = fb.Empty;

    if (!string.IsNullOrWhiteSpace(type)) filter &= fb.Eq(x => x.Type, type.Trim());
    if (!string.IsNullOrWhiteSpace(status)) filter &= fb.Eq(x => x.Status, status.Trim());
    if (minConnectors.HasValue) filter &= fb.Gte(x => x.Connectors, minConnectors.Value);

    if (!string.IsNullOrWhiteSpace(backOfficeNic))
    {
        var nic = backOfficeNic.Trim();
        var rx = new BsonRegularExpression("^\\s*" + Regex.Escape(nic) + "\\s*$", "i");
        filter &= fb.Or(fb.Eq(s => s.BackOfficeNic, nic), fb.Regex("backOfficeNic", rx));
    }

    if (!string.IsNullOrWhiteSpace(q))
    {
        var needle = Regex.Escape(q.Trim());
        var rx = new BsonRegularExpression(needle, "i");
        filter &= fb.Regex("name", rx);
    }

    var find = _stations.Find(filter, new FindOptions { Collation = CaseInsensitive })
                        .Sort(Builders<Station>.Sort.Descending(s => s.CreatedAtUtc));

    var total = await _stations.CountDocumentsAsync(filter, new CountOptions { Collation = CaseInsensitive }, ct);
    var items = await find.Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
    return (items, total);
}

        
    }
}
