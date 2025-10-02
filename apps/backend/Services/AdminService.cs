using System.Linq;
using EvCharge.Api.Domain;
using EvCharge.Api.Domain.DTOs;
using EvCharge.Api.Infrastructure.Errors;
using EvCharge.Api.Infrastructure.Validation;
using EvCharge.Api.Repositories;

namespace EvCharge.Api.Services
{
    public interface IAdminService
    {
        Task<OwnerResponse> CreateAdminAsync(AdminCreateRequest req, string actorNic, CancellationToken ct);
        Task<(List<OwnerResponse> items, long total)> ListBackOfficeApplicationsAsync(string? status, int page, int pageSize, CancellationToken ct);
        Task<OwnerResponse> ApproveBackOfficeAsync(string backOfficeNic, string reviewerNic, string? notes, CancellationToken ct);
        Task<OwnerResponse> RejectBackOfficeAsync(string backOfficeNic, string reviewerNic, string notes, CancellationToken ct);
    }

    public class AdminService : IAdminService
    {
        private readonly IEvOwnerRepository _owners;

        public AdminService(IEvOwnerRepository owners)
        {
            _owners = owners;
        }

        public async Task<OwnerResponse> CreateAdminAsync(AdminCreateRequest req, string actorNic, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.FullName) || req.FullName.Trim().Length is < 2 or > 120)
                throw new ValidationException("InvalidFullName", "Full name must be between 2 and 120 characters.");
            if (!EmailValidator.IsValid(req.Email))
                throw new ValidationException("InvalidEmail", "Email format is invalid.");
            if (!PasswordValidator.IsValid(req.Password))
                throw new ValidationException("WeakPassword", "Password must be at least 8 characters and include letters and numbers.");
            if (!PhoneValidator.IsValid(req.Phone))
                throw new ValidationException("InvalidPhone", "Phone format is invalid.");

            var email = req.Email.Trim();
            var emailLower = email.ToLowerInvariant();

            if (await _owners.ExistsByEmailLowerAsync(emailLower, ct))
                throw new RegistrationException("DuplicateEmail", "An account with this email already exists.");

            var now = DateTime.UtcNow;
            var hash = BCrypt.Net.BCrypt.HashPassword(req.Password);
            var nic = $"AD-{Guid.NewGuid():N}".Substring(0, 12);

            var o = new Owner
            {
                Nic = nic,
                FullName = req.FullName.Trim(),
                Email = email,
                EmailLower = emailLower,
                Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim(),
                PasswordHash = hash,
                IsActive = true,
                Roles = new List<string> { "Admin" },
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                UpdatedBy = actorNic
            };

            await _owners.CreateAsync(o, ct);

            return new OwnerResponse
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

        public async Task<(List<OwnerResponse> items, long total)> ListBackOfficeApplicationsAsync(string? status, int page, int pageSize, CancellationToken ct)
        {
            var (items, total) = await _owners.ListBackOfficesByStatusAsync(status, Math.Max(1, page), Math.Clamp(pageSize, 1, 100), ct);
            var list = items.Select(o => new OwnerResponse
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
            }).ToList();

            return (list, total);
        }

        public async Task<OwnerResponse> ApproveBackOfficeAsync(string backOfficeNic, string reviewerNic, string? notes, CancellationToken ct)
        {
            var o = await _owners.GetByNicAsync(backOfficeNic, ct) ?? throw new NotFoundException("BackOfficeNotFound", "BackOffice account not found.");
            if (!o.Roles.Contains("BackOffice") || o.BackOfficeProfile is null)
                throw new ValidationException("InvalidAccount", "Account is not a BackOffice or missing application.");

            o.BackOfficeProfile.ApplicationStatus = "Approved";
            o.BackOfficeProfile.ReviewedAtUtc = DateTime.UtcNow;
            o.BackOfficeProfile.ReviewedByNic = reviewerNic;
            o.BackOfficeProfile.ReviewNotes = string.IsNullOrWhiteSpace(notes) ? null : notes!.Trim();
            o.UpdatedAtUtc = o.BackOfficeProfile.ReviewedAtUtc;
            o.UpdatedBy = reviewerNic;

            var ok = await _owners.ReplaceAsync(o, ct);
            if (!ok) throw new UpdateException("ConcurrencyConflict", "Failed to approve. Please retry.");

            return new OwnerResponse
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

        public async Task<OwnerResponse> RejectBackOfficeAsync(string backOfficeNic, string reviewerNic, string notes, CancellationToken ct)
        {
            var o = await _owners.GetByNicAsync(backOfficeNic, ct) ?? throw new NotFoundException("BackOfficeNotFound", "BackOffice account not found.");
            if (!o.Roles.Contains("BackOffice") || o.BackOfficeProfile is null)
                throw new ValidationException("InvalidAccount", "Account is not a BackOffice or missing application.");
            if (string.IsNullOrWhiteSpace(notes))
                throw new ValidationException("ReviewNotesRequired", "Rejection requires notes.");

            o.BackOfficeProfile.ApplicationStatus = "Rejected";
            o.BackOfficeProfile.ReviewedAtUtc = DateTime.UtcNow;
            o.BackOfficeProfile.ReviewedByNic = reviewerNic;
            o.BackOfficeProfile.ReviewNotes = notes.Trim();
            o.UpdatedAtUtc = o.BackOfficeProfile.ReviewedAtUtc;
            o.UpdatedBy = reviewerNic;

            var ok = await _owners.ReplaceAsync(o, ct);
            if (!ok) throw new UpdateException("ConcurrencyConflict", "Failed to reject. Please retry.");

            return new OwnerResponse
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
}
