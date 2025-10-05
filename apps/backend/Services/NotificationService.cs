using EvCharge.Api.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EvCharge.Api.Services
{
    public interface INotificationService
    {
        Task EnqueueAsync(string type, string toNic, string subject, string message,
                          IDictionary<string, object?>? payload, CancellationToken ct);
    }

    public class NotificationService : INotificationService
    {
        private readonly IMongoCollection<Notification> _col;

        public NotificationService(IMongoDatabase db)
        {
            _col = db.GetCollection<Notification>("notifications");
            EnsureIndexes();
        }

        private void EnsureIndexes()
        {
            try
            {
                _col.Indexes.CreateMany(new[]
                {
                    new CreateIndexModel<Notification>(Builders<Notification>.IndexKeys
                        .Ascending(x => x.ToNic).Descending(x => x.CreatedAtUtc), new CreateIndexOptions{ Name="ix_to_created" }),
                    new CreateIndexModel<Notification>(Builders<Notification>.IndexKeys
                        .Descending(x => x.CreatedAtUtc), new CreateIndexOptions{ Name="ix_created_desc" })
                });
            }
            catch { /* idempotent */ }
        }

        public async Task EnqueueAsync(string type, string toNic, string subject, string message,
                                       IDictionary<string, object?>? payload, CancellationToken ct)
        {
            var n = new Notification
            {
                Type = type,
                ToNic = toNic,
                Subject = subject,
                Message = message,
                Payload = payload is null ? null : ToBson(payload),
                CreatedAtUtc = DateTime.UtcNow
            };
            await _col.InsertOneAsync(n, cancellationToken: ct);
        }

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
