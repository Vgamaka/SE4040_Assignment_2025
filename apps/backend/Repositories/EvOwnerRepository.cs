using EvCharge.Api.Domain;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;

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
        Task<(List<Owner> items, long total)> ListAllAsync(string? role, string? q, int page, int pageSize, CancellationToken ct);
        Task<(List<Owner> items, long total)> ListOperatorsByBackOfficeAsync(string backOfficeNic, int page, int pageSize, CancellationToken ct);
        Task<(List<Owner> items, long total)> ListBackOfficesByStatusAsync(string? status, int page, int pageSize, CancellationToken ct);
    }

    public class EvOwnerRepository : IEvOwnerRepository
    {
        private readonly IMongoCollection<Owner> _col;
        // Optional legacy collection fallback if older data was stored under "owners"
        private readonly IMongoCollection<Owner>? _legacyCol;
        private static readonly Collation CaseInsensitive = new("en", strength: CollationStrength.Secondary);
private readonly ILogger<EvOwnerRepository> _log;
    public EvOwnerRepository(IMongoDatabase db, ILogger<EvOwnerRepository> log) // 👈 inject
    {
        _log = log;
        _col = db.GetCollection<Owner>("ev_owners");
        try { _legacyCol = db.GetCollection<Owner>("owners"); } catch { _legacyCol = null; }
        EnsureIndexes(_col);
    }

        private static void EnsureIndexes(IMongoCollection<Owner> col)
        {
            var idxKeysNic = Builders<Owner>.IndexKeys.Ascending(x => x.Nic);
            var idxKeysEmailLower = Builders<Owner>.IndexKeys.Ascending(x => x.EmailLower);

            var models = new[]
            {
                new CreateIndexModel<Owner>(idxKeysNic,        new CreateIndexOptions { Unique = true, Name = "ux_nic" }),
                new CreateIndexModel<Owner>(idxKeysEmailLower, new CreateIndexOptions { Unique = true, Name = "ux_emailLower" })
            };

            try { col.Indexes.CreateMany(models); } catch { /* idempotent */ }
        }

        private static BsonRegularExpression ExactNicRegexWhitespaceTolerant(string nic)
            => new("^\\s*" + Regex.Escape(nic) + "\\s*$", "i");

        public async Task<bool> ExistsByNicAsync(string nic, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(nic)) return false;
            nic = nic.Trim();

            var fb = Builders<Owner>.Filter;
            var eq = fb.Eq(o => o.Nic, nic);

            // Primary collection: case-insensitive equality
            var find = _col.Find(eq, new FindOptions { Collation = CaseInsensitive });
            if (await find.AnyAsync(ct)) return true;

            // Primary collection: regex (case-insensitive) + tolerate stray whitespace in DB
            var rx = ExactNicRegexWhitespaceTolerant(nic);
            if (await _col.Find(fb.Regex("nic", rx)).AnyAsync(ct)) return true;

            // Fallback to legacy collection if present
            if (_legacyCol is not null)
            {
                var findLegacy = _legacyCol.Find(eq, new FindOptions { Collation = CaseInsensitive });
                if (await findLegacy.AnyAsync(ct)) return true;

                if (await _legacyCol.Find(fb.Regex("nic", rx)).AnyAsync(ct)) return true;
            }

            return false;
        }

        public async Task<bool> ExistsByEmailLowerAsync(string emailLower, CancellationToken ct)
            => await _col.Find(x => x.EmailLower == emailLower).AnyAsync(ct);

        public async Task<bool> ExistsByEmailLowerForDifferentNicAsync(string emailLower, string nic, CancellationToken ct)
            => await _col.Find(x => x.EmailLower == emailLower && x.Nic != nic).AnyAsync(ct);

        public async Task CreateAsync(Owner owner, CancellationToken ct)
            => await _col.InsertOneAsync(owner, cancellationToken: ct);

    public async Task<Owner?> GetByNicAsync(string nic, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(nic)) return null;
        nic = nic.Trim();

        _log.LogInformation("GetByNicAsync: db={Db}, coll={Coll}, nic='{Nic}'",
            _col.Database.DatabaseNamespace.DatabaseName,
            _col.CollectionNamespace.CollectionName, nic);

        var fb = Builders<Owner>.Filter;
        var eq = fb.Eq(o => o.Nic, nic);

        var owner = await _col.Find(eq, new FindOptions { Collation = CaseInsensitive })
                              .FirstOrDefaultAsync(ct);
        _log.LogInformation("GetByNicAsync(eq): found? {Found}", owner != null);
        if (owner is not null) return owner;

        var rx = new BsonRegularExpression("^\\s*" + Regex.Escape(nic) + "\\s*$", "i");
        owner = await _col.Find(fb.Regex("nic", rx)).FirstOrDefaultAsync(ct);
        _log.LogInformation("GetByNicAsync(regex): found? {Found}", owner != null);
        if (owner is not null) return owner;

        if (_legacyCol is not null)
        {
            owner = await _legacyCol.Find(eq, new FindOptions { Collation = CaseInsensitive })
                                    .FirstOrDefaultAsync(ct);
            _log.LogInformation("GetByNicAsync(legacy-eq): found? {Found}", owner != null);
            if (owner is not null) return owner;

            owner = await _legacyCol.Find(fb.Regex("nic", rx)).FirstOrDefaultAsync(ct);
            _log.LogInformation("GetByNicAsync(legacy-regex): found? {Found}", owner != null);
            if (owner is not null) return owner;
        }

        return null;
    }

        public async Task<Owner?> GetByEmailLowerAsync(string emailLower, CancellationToken ct)
            => await _col.Find(x => x.EmailLower == emailLower).FirstOrDefaultAsync(ct);

        public async Task<bool> ReplaceAsync(Owner owner, CancellationToken ct)
        {
            var res = await _col.ReplaceOneAsync(x => x.Nic == owner.Nic, owner, cancellationToken: ct);
            return res.ModifiedCount > 0;
        }

        // --------  queries for BackOffice and Operators --------
        public async Task<(List<Owner> items, long total)> ListOperatorsByBackOfficeAsync(
            string backOfficeNic, int page, int pageSize, CancellationToken ct)
        {
            backOfficeNic = (backOfficeNic ?? string.Empty).Trim();
            var fb = Builders<Owner>.Filter;
            // tolerate whitespace differences in stored backOfficeNic
            var rx = new BsonRegularExpression("^\\s*" + Regex.Escape(backOfficeNic) + "\\s*$", "i");

            var filter = fb.And(
                fb.AnyEq(o => o.Roles, "Operator"),
                fb.Or(
                    fb.Eq(o => o.BackOfficeNic, backOfficeNic),
                    fb.Regex("backOfficeNic", rx)
                )
            );

            var find  = _col.Find(filter, new FindOptions { Collation = CaseInsensitive });
            var total = await _col.CountDocumentsAsync(filter, new CountOptions { Collation = CaseInsensitive }, ct);
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

            var find  = _col.Find(filter);
            var total = await find.CountDocumentsAsync(ct);
            var items = await find.Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
            return (items, total);
        }
        public async Task<(List<Owner> items, long total)> ListAllAsync(
    string? role, string? q, int page, int pageSize, CancellationToken ct)
{
    var fb = Builders<Owner>.Filter;
    var filter = fb.Empty;

    if (!string.IsNullOrWhiteSpace(role))
        filter &= fb.AnyEq(o => o.Roles, role.Trim());

    if (!string.IsNullOrWhiteSpace(q))
    {
        var needle = Regex.Escape(q.Trim());
        var rx = new BsonRegularExpression(needle, "i");
        filter &= fb.Or(
            fb.Regex("nic", rx),
            fb.Regex("fullName", rx),
            fb.Regex("email", rx)
        );
    }

    var find = _col.Find(filter, new FindOptions { Collation = CaseInsensitive })
                   .Sort(Builders<Owner>.Sort.Descending(o => o.CreatedAtUtc));

    var total = await _col.CountDocumentsAsync(filter, new CountOptions { Collation = CaseInsensitive }, ct);
    var items = await find.Skip((page - 1) * pageSize).Limit(pageSize).ToListAsync(ct);
    return (items, total);
}


    }
}
