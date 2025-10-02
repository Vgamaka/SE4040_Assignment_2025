using EvCharge.Api.Domain;
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

        // schedules
        Task<StationSchedule?> GetScheduleAsync(string stationId, CancellationToken ct);
        Task UpsertScheduleAsync(StationSchedule schedule, CancellationToken ct);

        // NEW: scoping by BackOffice (4-arg signature)
        Task<(List<Station> items, long total)> ListByBackOfficeAsync(string backOfficeNic, int page, int pageSize, CancellationToken ct);
    }

    public class StationRepository : IStationRepository
    {
        private readonly IMongoCollection<Station> _stations;
        private readonly IMongoCollection<StationSchedule> _schedules;

        public StationRepository(IMongoDatabase db)
        {
            _stations = db.GetCollection<Station>("stations");
            _schedules = db.GetCollection<StationSchedule>("station_schedules");
            EnsureIndexes();
        }

        private void EnsureIndexes()
        {
            try
            {
                // 2dsphere on Location
                var geo = Builders<Station>.IndexKeys.Geo2DSphere("Location");
                _stations.Indexes.CreateOne(new CreateIndexModel<Station>(geo, new CreateIndexOptions { Name = "ix_Location_2dsphere" }));

                var st = Builders<Station>.IndexKeys.Ascending(x => x.Status);
                var tp = Builders<Station>.IndexKeys.Ascending(x => x.Type);
                var bo = Builders<Station>.IndexKeys.Ascending(x => x.BackOfficeNic);
                _stations.Indexes.CreateMany(new[]
                {
                    new CreateIndexModel<Station>(st, new CreateIndexOptions{ Name="ix_status" }),
                    new CreateIndexModel<Station>(tp, new CreateIndexOptions{ Name="ix_type" }),
                    new CreateIndexModel<Station>(bo, new CreateIndexOptions{ Name="ix_backOfficeNic" })
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

            var find = _stations.Find(filter);
            var total = await find.CountDocumentsAsync(ct);
            var items = await find.Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
            return (items, total);
        }

        public async Task<List<Station>> NearbyAsync(double lat, double lng, double radiusKm, string? type, CancellationToken ct)
        {
            var fb = Builders<Station>.Filter;
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

        // NEW
        public async Task<(List<Station> items, long total)> ListByBackOfficeAsync(string backOfficeNic, int page, int pageSize, CancellationToken ct)
        {
            var fb = Builders<Station>.Filter;
            var filter = fb.Eq(x => x.BackOfficeNic, backOfficeNic);

            var find = _stations.Find(filter);
            var total = await find.CountDocumentsAsync(ct);
            var items = await find.Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
            return (items, total);
        }
    }
}
