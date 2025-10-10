using EvCharge.Api.Domain;
using MongoDB.Bson;
using EvCharge.Api.Repositories;

namespace EvCharge.Api.Services
{
    public interface IAuditService
    {
        Task LogAsync(string entityType, string entityId, string action,
                      string? actorNic, string? actorRole,
                      IDictionary<string, object?>? payload, CancellationToken ct);

        Task<(List<AuditEvent> items, long total)> SearchAsync(
            string? entityType, string? entityId, string? action, string? actor,
            DateTime? fromUtc, DateTime? toUtc, int page, int pageSize, CancellationToken ct);
    }

    public class AuditService : IAuditService
    {
        private readonly IAuditRepository _repo;
        public AuditService(IAuditRepository repo) { _repo = repo; }

        public async Task LogAsync(string entityType, string entityId, string action,
                                   string? actorNic, string? actorRole,
                                   IDictionary<string, object?>? payload, CancellationToken ct)
        {
            var doc = payload is null ? null : ToBson(payload);
            var e = new AuditEvent
            {
                EntityType = entityType,
                EntityId = entityId,
                Action = action,
                ActorNic = actorNic,
                ActorRole = actorRole,
                Payload = doc,
                CreatedAtUtc = DateTime.UtcNow
            };
            await _repo.CreateAsync(e, ct);
        }

        public Task<(List<AuditEvent> items, long total)> SearchAsync(
            string? entityType, string? entityId, string? action, string? actor,
            DateTime? fromUtc, DateTime? toUtc, int page, int pageSize, CancellationToken ct)
            => _repo.SearchAsync(entityType, entityId, action, actor, fromUtc, toUtc, page, pageSize, ct);

        private static BsonDocument ToBson(IDictionary<string, object?> payload)
        {
            var doc = new BsonDocument();
            foreach (var kv in payload)
            {
                doc[kv.Key] = kv.Value is null ? BsonNull.Value : BsonValue.Create(kv.Value);
            }
            return doc;
        }
    }
}
