using EvCharge.Api.Domain;
using EvCharge.Api.Domain.DTOs;
using EvCharge.Api.Infrastructure.Errors;
using EvCharge.Api.Infrastructure.Validation;
using EvCharge.Api.Repositories;

namespace EvCharge.Api.Services
{
    public interface IOwnerService
    {
        Task<OwnerResponse> RegisterAsync(OwnerRegisterRequest req, CancellationToken ct);
        Task<OwnerResponse> GetByNicAsync(string nic, CancellationToken ct);
        Task<OwnerResponse> UpdateAsync(string nic, OwnerUpdateRequest req, string actorNic, CancellationToken ct);
        Task<OwnerResponse> DeactivateAsync(string nic, string actorNic, CancellationToken ct);
        Task<OwnerResponse> ReactivateAsync(string nic, string actorNic, CancellationToken ct);
    }

    public class OwnerService : IOwnerService
    {
        private readonly IEvOwnerRepository _repo;

        public OwnerService(IEvOwnerRepository repo) => _repo = repo;

        public async Task<OwnerResponse> RegisterAsync(OwnerRegisterRequest req, CancellationToken ct)
        {
            if (!NicValidator.IsValid(req.Nic))
                throw new RegistrationException("InvalidNic", "NIC format is invalid.");
            if (string.IsNullOrWhiteSpace(req.FullName) || req.FullName.Trim().Length is < 2 or > 120)
                throw new RegistrationException("InvalidFullName", "Full name must be between 2 and 120 characters.");
            if (!EmailValidator.IsValid(req.Email))
                throw new RegistrationException("InvalidEmail", "Email format is invalid.");
            if (!PasswordValidator.IsValid(req.Password))
                throw new RegistrationException("WeakPassword", "Password must be at least 8 characters and include letters and numbers.");
            if (!PhoneValidator.IsValid(req.Phone))
                throw new RegistrationException("InvalidPhone", "Phone format is invalid.");

            var nic = req.Nic.Trim();
            var email = req.Email.Trim();
            var emailLower = email.ToLowerInvariant();

            if (await _repo.ExistsByNicAsync(nic, ct))
                throw new RegistrationException("DuplicateNic", "An owner with this NIC already exists.");
            if (await _repo.ExistsByEmailLowerAsync(emailLower, ct))
                throw new RegistrationException("DuplicateEmail", "An owner with this email already exists.");

            var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);

            var owner = new Owner
            {
                Nic = nic,
                FullName = req.FullName.Trim(),
                Email = email,
                EmailLower = emailLower,
                Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim(),
                PasswordHash = hash,
                Address = (string.IsNullOrWhiteSpace(req.AddressLine1) &&
                           string.IsNullOrWhiteSpace(req.AddressLine2) &&
                           string.IsNullOrWhiteSpace(req.City))
                           ? null
                           : new Address
                           {
                               Line1 = string.IsNullOrWhiteSpace(req.AddressLine1) ? null : req.AddressLine1.Trim(),
                               Line2 = string.IsNullOrWhiteSpace(req.AddressLine2) ? null : req.AddressLine2.Trim(),
                               City  = string.IsNullOrWhiteSpace(req.City) ? null : req.City.Trim()
                           },
                IsActive = true,
                Roles = new List<string> { "Owner" },
                CreatedAtUtc = DateTime.UtcNow
            };

            await _repo.CreateAsync(owner, ct);
            return ToResponse(owner);
        }

        public async Task<OwnerResponse> GetByNicAsync(string nic, CancellationToken ct)
        {
            if (!NicValidator.IsValid(nic))
                throw new NotFoundException("OwnerNotFound", "Owner not found.");

            var owner = await _repo.GetByNicAsync(nic.Trim(), ct);
            if (owner is null)
                throw new NotFoundException("OwnerNotFound", "Owner not found.");

            return ToResponse(owner);
        }

        public async Task<OwnerResponse> UpdateAsync(string nic, OwnerUpdateRequest req, string actorNic, CancellationToken ct)
        {
            if (!NicValidator.IsValid(nic))
                throw new NotFoundException("OwnerNotFound", "Owner not found.");

            var owner = await _repo.GetByNicAsync(nic.Trim(), ct);
            if (owner is null)
                throw new NotFoundException("OwnerNotFound", "Owner not found.");

            if (!string.IsNullOrWhiteSpace(req.FullName))
            {
                var fn = req.FullName.Trim();
                if (fn.Length is < 2 or > 120)
                    throw new UpdateException("InvalidFullName", "Full name must be between 2 and 120 characters.");
                owner.FullName = fn;
            }

            if (!string.IsNullOrWhiteSpace(req.Email))
            {
                var email = req.Email.Trim();
                if (!EmailValidator.IsValid(email))
                    throw new UpdateException("InvalidEmail", "Email format is invalid.");
                var emailLower = email.ToLowerInvariant();
                if (!string.Equals(owner.EmailLower, emailLower, StringComparison.Ordinal))
                {
                    var taken = await _repo.ExistsByEmailLowerForDifferentNicAsync(emailLower, owner.Nic, ct);
                    if (taken) throw new UpdateException("DuplicateEmail", "An owner with this email already exists.");
                    owner.Email = email;
                    owner.EmailLower = emailLower;
                }
            }

            if (req.Phone != null)
            {
                var phoneTrim = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
                if (!PhoneValidator.IsValid(phoneTrim))
                    throw new UpdateException("InvalidPhone", "Phone format is invalid.");
                owner.Phone = phoneTrim;
            }

            if (req.AddressLine1 != null || req.AddressLine2 != null || req.City != null)
            {
                var l1 = string.IsNullOrWhiteSpace(req.AddressLine1) ? null : req.AddressLine1.Trim();
                var l2 = string.IsNullOrWhiteSpace(req.AddressLine2) ? null : req.AddressLine2.Trim();
                var city = string.IsNullOrWhiteSpace(req.City) ? null : req.City.Trim();

                if (l1 is null && l2 is null && city is null) owner.Address = null;
                else
                {
                    owner.Address ??= new Address();
                    owner.Address.Line1 = l1; owner.Address.Line2 = l2; owner.Address.City = city;
                }
            }

            owner.UpdatedAtUtc = DateTime.UtcNow;
            owner.UpdatedBy = actorNic;

            var ok = await _repo.ReplaceAsync(owner, ct);
            if (!ok) throw new UpdateException("ConcurrencyConflict", "Failed to update owner. Please retry.");

            return ToResponse(owner);
        }

        public async Task<OwnerResponse> DeactivateAsync(string nic, string actorNic, CancellationToken ct)
        {
            if (!NicValidator.IsValid(nic))
                throw new NotFoundException("OwnerNotFound", "Owner not found.");

            var owner = await _repo.GetByNicAsync(nic.Trim(), ct);
            if (owner is null)
                throw new NotFoundException("OwnerNotFound", "Owner not found.");

            if (!owner.IsActive) return ToResponse(owner);

            owner.IsActive = false;
            owner.DeactivatedAtUtc = DateTime.UtcNow;
            owner.DeactivatedBy = actorNic;
            owner.UpdatedAtUtc = owner.DeactivatedAtUtc;
            owner.UpdatedBy = actorNic;

            var ok = await _repo.ReplaceAsync(owner, ct);
            if (!ok) throw new UpdateException("ConcurrencyConflict", "Failed to deactivate. Please retry.");

            return ToResponse(owner);
        }

        public async Task<OwnerResponse> ReactivateAsync(string nic, string actorNic, CancellationToken ct)
        {
            if (!NicValidator.IsValid(nic))
                throw new NotFoundException("OwnerNotFound", "Owner not found.");

            var owner = await _repo.GetByNicAsync(nic.Trim(), ct);
            if (owner is null)
                throw new NotFoundException("OwnerNotFound", "Owner not found.");

            if (owner.IsActive) return ToResponse(owner);

            owner.IsActive = true;
            owner.DeactivatedAtUtc = null;
            owner.DeactivatedBy = null;
            owner.UpdatedAtUtc = DateTime.UtcNow;
            owner.UpdatedBy = actorNic;

            var ok = await _repo.ReplaceAsync(owner, ct);
            if (!ok) throw new UpdateException("ConcurrencyConflict", "Failed to reactivate. Please retry.");

            return ToResponse(owner);
        }

        private static OwnerResponse ToResponse(Owner o) => new OwnerResponse
        {
            Nic = o.Nic,
            FullName = o.FullName,
            Email = o.Email,
            Phone = o.Phone,
            Address = o.Address,
            IsActive = o.IsActive,
            Roles = o.Roles,
            CreatedAtUtc = o.CreatedAtUtc,
            UpdatedAtUtc = o.UpdatedAtUtc
        };
    }
}
