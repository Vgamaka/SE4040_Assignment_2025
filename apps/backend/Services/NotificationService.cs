using EvCharge.Api.Domain;
using MongoDB.Bson;
using MongoDB.Driver;

namespace EvCharge.Api.Services
{
    public interface INotificationService
    {
        Task EnqueueAsync(string type, string toNic, string subject, string message,
                          IDictionary<string, object?>? payload, CancellationToken ct);

        Task<(List<Notification> items, long total)> ListMineAsync(string nic, bool? unreadOnly, int page, int pageSize, CancellationToken ct);
        Task<bool> MarkReadAsync(string id, string nic, CancellationToken ct);
        Task<long> MarkAllReadAsync(string nic, CancellationToken ct);
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
                    new CreateIndexModel<Notification>(
                        Builders<Notification>.IndexKeys
                            .Ascending(x => x.ToNic)
                            .Descending(x => x.CreatedAtUtc),
                        new CreateIndexOptions { Name = "ix_to_created" }),

                    new CreateIndexModel<Notification>(
                        Builders<Notification>.IndexKeys
                            .Descending(x => x.CreatedAtUtc),
                        new CreateIndexOptions { Name = "ix_created_desc" }),

                    //  fast unread queries per user
                    new CreateIndexModel<Notification>(
                        Builders<Notification>.IndexKeys
                            .Ascending(x => x.ToNic)
                            .Ascending(x => x.ReadAtUtc),
                        new CreateIndexOptions { Name = "ix_to_read" })
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

        public async Task<(List<Notification> items, long total)> ListMineAsync(
            string nic, bool? unreadOnly, int page, int pageSize, CancellationToken ct)
        {
            var fb = Builders<Notification>.Filter;
            var filter = fb.Eq(x => x.ToNic, nic);
            if (unreadOnly == true)
                filter = fb.And(filter, fb.Eq(x => x.ReadAtUtc, (DateTime?)null));

            var find = _col.Find(filter).SortByDescending(x => x.CreatedAtUtc);
            var total = await _col.CountDocumentsAsync(filter, cancellationToken: ct);
            var items = await find.Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
            return (items, total);
        }

        public async Task<bool> MarkReadAsync(string id, string nic, CancellationToken ct)
        {
            var fb = Builders<Notification>.Filter;
            var filter = fb.And(
                fb.Eq(x => x.Id, id),
                fb.Eq(x => x.ToNic, nic),
                fb.Eq(x => x.ReadAtUtc, (DateTime?)null)
            );

            var update = Builders<Notification>.Update
                .Set(x => x.ReadAtUtc, DateTime.UtcNow);

            var res = await _col.UpdateOneAsync(filter, update, cancellationToken: ct);
            return res.ModifiedCount > 0;
        }

        public async Task<long> MarkAllReadAsync(string nic, CancellationToken ct)
        {
            var fb = Builders<Notification>.Filter;
            var filter = fb.And(
                fb.Eq(x => x.ToNic, nic),
                fb.Eq(x => x.ReadAtUtc, (DateTime?)null)
            );

            var update = Builders<Notification>.Update
                .Set(x => x.ReadAtUtc, DateTime.UtcNow);

            var res = await _col.UpdateManyAsync(filter, update, cancellationToken: ct);
            return res.ModifiedCount;
        }

        private static BsonDocument ToBson(IDictionary<string, object?> payload)
        {
            var doc = new BsonDocument();
            foreach (var kv in payload)
                doc[kv.Key] = kv.Value is null ? BsonNull.Value : BsonValue.Create(kv.Value);
            return doc;
        }
    }
}
