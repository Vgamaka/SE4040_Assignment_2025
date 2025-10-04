using EvCharge.Api.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EvCharge.Api.Repositories
{
    public interface IAuditRepository
    {
        Task EnsureIndexesAsync(CancellationToken ct);
        Task CreateAsync(AuditEvent e, CancellationToken ct);
        Task<(List<AuditEvent> items, long total)> SearchAsync(
            string? entityType, string? entityId, string? action, string? actor, DateTime? fromUtc, DateTime? toUtc,
            int page, int pageSize, CancellationToken ct);
    }

    public class AuditRepository : IAuditRepository
    {
        private readonly IMongoCollection<AuditEvent> _col;

        public AuditRepository(IMongoDatabase db)
        {
            _col = db.GetCollection<AuditEvent>("audits");
            EnsureIndexesAsync(CancellationToken.None).GetAwaiter().GetResult();
        }

        public async Task EnsureIndexesAsync(CancellationToken ct)
        {
            try
            {
                await _col.Indexes.CreateManyAsync(new[]
                {
                    new CreateIndexModel<AuditEvent>(
                        Builders<AuditEvent>.IndexKeys.Ascending(x => x.EntityType).Ascending(x => x.EntityId)
                                                   .Descending(x => x.CreatedAtUtc),
                        new CreateIndexOptions { Name = "ix_entity_created_desc" }),
                    new CreateIndexModel<AuditEvent>(
                        Builders<AuditEvent>.IndexKeys.Descending(x => x.CreatedAtUtc),
                        new CreateIndexOptions { Name = "ix_created_desc" }),
                    new CreateIndexModel<AuditEvent>(
                        Builders<AuditEvent>.IndexKeys.Ascending(x => x.Action),
                        new CreateIndexOptions { Name = "ix_action" }),
                    new CreateIndexModel<AuditEvent>(
                        Builders<AuditEvent>.IndexKeys.Ascending(x => x.ActorNic),
                        new CreateIndexOptions { Name = "ix_actor" })
                }, cancellationToken: ct);
            }
            catch { /* idempotent */ }
        }

        public async Task CreateAsync(AuditEvent e, CancellationToken ct)
            => await _col.InsertOneAsync(e, cancellationToken: ct);

        public async Task<(List<AuditEvent> items, long total)> SearchAsync(
            string? entityType, string? entityId, string? action, string? actor, DateTime? fromUtc, DateTime? toUtc,
            int page, int pageSize, CancellationToken ct)
        {
            var fb = Builders<AuditEvent>.Filter;
            var f = fb.Empty;

            if (!string.IsNullOrWhiteSpace(entityType)) f &= fb.Eq(x => x.EntityType, entityType);
            if (!string.IsNullOrWhiteSpace(entityId))   f &= fb.Eq(x => x.EntityId, entityId);
            if (!string.IsNullOrWhiteSpace(action))     f &= fb.Eq(x => x.Action, action);
            if (!string.IsNullOrWhiteSpace(actor))      f &= fb.Regex(x => x.ActorNic, new BsonRegularExpression(actor, "i"));
            if (fromUtc.HasValue)                       f &= fb.Gte(x => x.CreatedAtUtc, fromUtc.Value);
            if (toUtc.HasValue)                         f &= fb.Lte(x => x.CreatedAtUtc, toUtc.Value);

            var find  = _col.Find(f).SortByDescending(x => x.CreatedAtUtc);
            var total = await find.CountDocumentsAsync(ct);
            var items = await find.Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
            return (items, total);
        }
    }
}
