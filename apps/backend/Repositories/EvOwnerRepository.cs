using EvCharge.Api.Domain;
using MongoDB.Driver;

namespace EvCharge.Api.Repositories
{
    public interface IEvOwnerRepository
    {
        Task<bool> ExistsByNicAsync(string nic, CancellationToken ct);
        Task<bool> ExistsByEmailLowerAsync(string emailLower, CancellationToken ct);
        Task<bool> ExistsByEmailLowerForDifferentNicAsync(string emailLower, string nic, CancellationToken ct);
        Task CreateAsync(Owner owner, CancellationToken ct);
        Task<Owner?> GetByNicAsync(string nic, CancellationToken ct);
        Task<bool> ReplaceAsync(Owner owner, CancellationToken ct);
    }

    public class EvOwnerRepository : IEvOwnerRepository
    {
        private readonly IMongoCollection<Owner> _col;

        public EvOwnerRepository(IMongoDatabase db)
        {
            _col = db.GetCollection<Owner>("ev_owners");
            EnsureIndexes(_col);
        }

        private static void EnsureIndexes(IMongoCollection<Owner> col)
        {
            var idxKeysNic = Builders<Owner>.IndexKeys.Ascending(x => x.Nic);
            var idxKeysEmailLower = Builders<Owner>.IndexKeys.Ascending(x => x.EmailLower);

            var models = new[]
            {
                new CreateIndexModel<Owner>(idxKeysNic, new CreateIndexOptions { Unique = true, Name = "ux_nic" }),
                new CreateIndexModel<Owner>(idxKeysEmailLower, new CreateIndexOptions { Unique = true, Name = "ux_emailLower" })
            };

            try { col.Indexes.CreateMany(models); } catch { /* idempotent */ }
        }

        public async Task<bool> ExistsByNicAsync(string nic, CancellationToken ct)
            => await _col.Find(x => x.Nic == nic).AnyAsync(ct);

        public async Task<bool> ExistsByEmailLowerAsync(string emailLower, CancellationToken ct)
            => await _col.Find(x => x.EmailLower == emailLower).AnyAsync(ct);

        public async Task<bool> ExistsByEmailLowerForDifferentNicAsync(string emailLower, string nic, CancellationToken ct)
            => await _col.Find(x => x.EmailLower == emailLower && x.Nic != nic).AnyAsync(ct);

        public async Task CreateAsync(Owner owner, CancellationToken ct)
            => await _col.InsertOneAsync(owner, cancellationToken: ct);

        public async Task<Owner?> GetByNicAsync(string nic, CancellationToken ct)
            => await _col.Find(x => x.Nic == nic).FirstOrDefaultAsync(ct);

        public async Task<bool> ReplaceAsync(Owner owner, CancellationToken ct)
        {
            var res = await _col.ReplaceOneAsync(x => x.Nic == owner.Nic, owner, cancellationToken: ct);
            return res.ModifiedCount > 0;
        }
    }
}
