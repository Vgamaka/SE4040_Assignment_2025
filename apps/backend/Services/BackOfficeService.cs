using System.Linq;
using EvCharge.Api.Domain;
using EvCharge.Api.Domain.DTOs;
using EvCharge.Api.Infrastructure.Errors;
using EvCharge.Api.Infrastructure.Validation;
using EvCharge.Api.Repositories;

namespace EvCharge.Api.Services
{
    public interface IBackOfficeService
    {
        Task<OwnerResponse> ApplyAsync(BackOfficeApplyRequest req, CancellationToken ct);
        Task<OwnerResponse> GetMyProfileAsync(string nic, CancellationToken ct);
        Task<(List<OwnerResponse> items, long total)> ListOperatorsAsync(string backOfficeNic, int page, int pageSize, CancellationToken ct);
        Task<OwnerResponse> CreateOperatorAsync(OperatorCreateRequest req, string backOfficeNic, CancellationToken ct);
        Task<OwnerResponse> AttachStationsToOperatorAsync(string operatorNic, List<string> stationIds, string backOfficeNic, CancellationToken ct);
    }

    public class BackOfficeService : IBackOfficeService
    {
        private readonly IEvOwnerRepository _owners;
        private readonly IStationRepository _stations;

        public BackOfficeService(IEvOwnerRepository owners, IStationRepository stations)
        {
            _owners = owners; _stations = stations;
        }

        public async Task<OwnerResponse> ApplyAsync(BackOfficeApplyRequest req, CancellationToken ct)
        {
            // Basic validation
            if (string.IsNullOrWhiteSpace(req.FullName) || req.FullName.Trim().Length is < 2 or > 120)
                throw new ValidationException("InvalidFullName", "Full name must be between 2 and 120 characters.");
            if (!EmailValidator.IsValid(req.Email))
                throw new ValidationException("InvalidEmail", "Email format is invalid.");
            if (!PasswordValidator.IsValid(req.Password))
                throw new ValidationException("WeakPassword", "Password must be at least 8 characters and include letters and numbers.");
            if (!PhoneValidator.IsValid(req.Phone))
                throw new ValidationException("InvalidPhone", "Phone format is invalid.");

            var businessName = req.BusinessName?.Trim();
            if (string.IsNullOrWhiteSpace(businessName) || businessName.Length is < 2 or > 160)
                throw new ValidationException("InvalidBusinessName", "Business name must be between 2 and 160 characters.");

            var contactEmail = req.ContactEmail?.Trim();
            if (!EmailValidator.IsValid(contactEmail ?? string.Empty))
                throw new ValidationException("InvalidContactEmail", "Contact email is invalid.");

            if (req.ContactPhone is not null && !PhoneValidator.IsValid(req.ContactPhone))
                throw new ValidationException("InvalidContactPhone", "Contact phone is invalid.");

            var email = req.Email.Trim();
            var emailLower = email.ToLowerInvariant();

            if (await _owners.ExistsByEmailLowerAsync(emailLower, ct))
                throw new RegistrationException("DuplicateEmail", "An account with this email already exists.");

            var now  = DateTime.UtcNow;
            var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);
            var nic  = "BO-" + Guid.NewGuid().ToString("N")[..9].ToUpperInvariant(); // e.g., BO-4DA2EC2A0

            var profile = new BackOfficeProfile
            {
                BusinessName      = businessName!,
                ContactEmail      = contactEmail!,
                ContactPhone      = string.IsNullOrWhiteSpace(req.ContactPhone) ? null : req.ContactPhone.Trim(),
                ApplicationStatus = "Pending",
                SubmittedAtUtc    = now
            };

            var o = new Owner
            {
                Nic               = nic,
                FullName          = req.FullName.Trim(),
                Email             = email,
                EmailLower        = emailLower,
                Phone             = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim(),
                PasswordHash      = hash,
                Address           = null,
                IsActive          = true, // active login; actions gated by Admin approval
                Roles             = new List<string> { "BackOffice" },
                BackOfficeProfile = profile,
                CreatedAtUtc      = now
            };

            await _owners.CreateAsync(o, ct);

            return new OwnerResponse
            {
                Nic          = o.Nic,
                FullName     = o.FullName,
                Email        = o.Email,
                Phone        = o.Phone,
                Address      = o.Address,
                IsActive     = o.IsActive,
                Roles        = o.Roles,
                CreatedAtUtc = o.CreatedAtUtc,
                UpdatedAtUtc = o.UpdatedAtUtc
            };
        }

        public async Task<OwnerResponse> GetMyProfileAsync(string nic, CancellationToken ct)
        {
            var o = await _owners.GetByNicAsync(nic, ct) ?? throw new NotFoundException("BackOfficeNotFound", "BackOffice account not found.");
            return new OwnerResponse
            {
                Nic          = o.Nic,
                FullName     = o.FullName,
                Email        = o.Email,
                Phone        = o.Phone,
                Address      = o.Address,
                IsActive     = o.IsActive,
                Roles        = o.Roles,
                CreatedAtUtc = o.CreatedAtUtc,
                UpdatedAtUtc = o.UpdatedAtUtc
            };
        }

        public async Task<(List<OwnerResponse> items, long total)> ListOperatorsAsync(string backOfficeNic, int page, int pageSize, CancellationToken ct)
        {
            var (items, total) = await _owners.ListOperatorsByBackOfficeAsync(backOfficeNic, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);
            var list = items.Select(o => new OwnerResponse
            {
                Nic          = o.Nic,
                FullName     = o.FullName,
                Email        = o.Email,
                Phone        = o.Phone,
                Address      = o.Address,
                IsActive     = o.IsActive,
                Roles        = o.Roles,
                CreatedAtUtc = o.CreatedAtUtc,
                UpdatedAtUtc = o.UpdatedAtUtc
            }).ToList();
            return (list, total);
        }

        public async Task<OwnerResponse> CreateOperatorAsync(OperatorCreateRequest req, string backOfficeNic, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.FullName) || req.FullName.Trim().Length is < 2 or > 120)
                throw new ValidationException("InvalidFullName", "Full name must be between 2 and 120 characters.");
            if (!EmailValidator.IsValid(req.Email))
                throw new ValidationException("InvalidEmail", "Email format is invalid.");
            if (!PasswordValidator.IsValid(req.Password))
                throw new ValidationException("WeakPassword", "Password must be at least 8 characters and include letters and numbers.");
            if (!PhoneValidator.IsValid(req.Phone))
                throw new ValidationException("InvalidPhone", "Phone format is invalid.");

            var email      = req.Email.Trim();
            var emailLower = email.ToLowerInvariant();

            if (await _owners.ExistsByEmailLowerAsync(emailLower, ct))
                throw new RegistrationException("DuplicateEmail", "An account with this email already exists.");

            var stationIds = (req.StationIds ?? new List<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct()
                .ToList();

            foreach (var sid in stationIds)
            {
                // First check: does this station belong to me?
                var belongs = await _stations.BelongsToBackOfficeAsync(sid, backOfficeNic, ct);
                if (!belongs)
                {
                    // Decide if it's "not found" vs "forbidden"
                    var exists = await _stations.GetByIdAsync(sid, ct) is not null;
                    if (!exists)
                        throw new NotFoundException("StationNotFound", $"Station {sid} not found.");
                    throw new UpdateException("ForbiddenStationScope", $"Station {sid} does not belong to this BackOffice.");
                }
            }

            var now  = DateTime.UtcNow;
            var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);
            var nic  = "OP-" + Guid.NewGuid().ToString("N")[..9].ToUpperInvariant();

            var normalizedBackOfficeNic = (backOfficeNic ?? string.Empty).Trim().ToUpperInvariant();
            var o = new Owner
            {
                Nic                = nic,
                FullName           = req.FullName.Trim(),
                Email              = email,
                EmailLower         = emailLower,
                Phone              = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim(),
                PasswordHash       = hash,
                IsActive           = true,
                Roles              = new List<string> { "Operator" },
                BackOfficeNic      = normalizedBackOfficeNic,
                OperatorStationIds = stationIds,
                CreatedAtUtc       = now
            };

            await _owners.CreateAsync(o, ct);

            return new OwnerResponse
            {
                Nic          = o.Nic,
                FullName     = o.FullName,
                Email        = o.Email,
                Phone        = o.Phone,
                Address      = o.Address,
                IsActive     = o.IsActive,
                Roles        = o.Roles,
                CreatedAtUtc = o.CreatedAtUtc,
                UpdatedAtUtc = o.UpdatedAtUtc
            };
        }

        public async Task<OwnerResponse> AttachStationsToOperatorAsync(string operatorNic, List<string> stationIds, string backOfficeNic, CancellationToken ct)
        {
            var op = await _owners.GetByNicAsync(operatorNic, ct) ?? throw new NotFoundException("OperatorNotFound", "Operator not found.");
            if (!op.Roles.Contains("Operator"))
                throw new ValidationException("InvalidRole", "Target user is not an operator.");
if (!string.Equals(op.BackOfficeNic?.Trim() ?? "", backOfficeNic.Trim(), StringComparison.OrdinalIgnoreCase))
    throw new UpdateException("Forbidden", "Operator does not belong to this BackOffice.");

            var cleaned = (stationIds ?? new List<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim())
                .Distinct()
                .ToList();

            foreach (var sid in cleaned)
            {
                var belongs = await _stations.BelongsToBackOfficeAsync(sid, backOfficeNic, ct);
                if (!belongs)
                {
                    var exists = await _stations.GetByIdAsync(sid, ct) is not null;
                    if (!exists)
                        throw new NotFoundException("StationNotFound", $"Station {sid} not found.");
                    throw new UpdateException("ForbiddenStationScope", $"Station {sid} does not belong to this BackOffice.");
                }
            }

            op.OperatorStationIds = cleaned;
            op.UpdatedAtUtc = DateTime.UtcNow;
            op.UpdatedBy = backOfficeNic;

            var ok = await _owners.ReplaceAsync(op, ct);
            if (!ok) throw new UpdateException("ConcurrencyConflict", "Failed to update operator stations. Please retry.");

            return new OwnerResponse
            {
                Nic          = op.Nic,
                FullName     = op.FullName,
                Email        = op.Email,
                Phone        = op.Phone,
                Address      = op.Address,
                IsActive     = op.IsActive,
                Roles        = op.Roles,
                CreatedAtUtc = op.CreatedAtUtc,
                UpdatedAtUtc = op.UpdatedAtUtc
            };
        }
    }
}
