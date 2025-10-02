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
        Task<Owner?> GetByEmailLowerAsync(string emailLower, CancellationToken ct);
        Task<bool> ReplaceAsync(Owner owner, CancellationToken ct);

        // NEW
        Task<(List<Owner> items, long total)> ListOperatorsByBackOfficeAsync(string backOfficeNic, int page, int pageSize, CancellationToken ct);
        Task<(List<Owner> items, long total)> ListBackOfficesByStatusAsync(string? status, int page, int pageSize, CancellationToken ct);
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

        public async Task<Owner?> GetByEmailLowerAsync(string emailLower, CancellationToken ct)
            => await _col.Find(x => x.EmailLower == emailLower).FirstOrDefaultAsync(ct);

        public async Task<bool> ReplaceAsync(Owner owner, CancellationToken ct)
        {
            var res = await _col.ReplaceOneAsync(x => x.Nic == owner.Nic, owner, cancellationToken: ct);
            return res.ModifiedCount > 0;
        }

        // -------- NEW: queries for BackOffice and Operators --------
        public async Task<(List<Owner> items, long total)> ListOperatorsByBackOfficeAsync(string backOfficeNic, int page, int pageSize, CancellationToken ct)
        {
            var fb = Builders<Owner>.Filter;
            // Use a Where expression to avoid AnyEq generic headaches on some driver versions
            var filter = fb.Where(o => o.Roles.Contains("Operator") && o.BackOfficeNic == backOfficeNic);

            var find = _col.Find(filter);
            var total = await find.CountDocumentsAsync(ct);
            var items = await find.Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
            return (items, total);
        }

        public async Task<(List<Owner> items, long total)> ListBackOfficesByStatusAsync(string? status, int page, int pageSize, CancellationToken ct)
        {
            var fb = Builders<Owner>.Filter;
            var filter = fb.Where(o => o.Roles.Contains("BackOffice") && o.BackOfficeProfile != null);
            if (!string.IsNullOrWhiteSpace(status))
            {
                filter = fb.And(filter, fb.Eq(o => o.BackOfficeProfile!.ApplicationStatus, status));
            }

            var find = _col.Find(filter);
            var total = await find.CountDocumentsAsync(ct);
            var items = await find.Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
            return (items, total);
        }
    }
}
