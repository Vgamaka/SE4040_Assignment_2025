using EvCharge.Api.Domain;
using EvCharge.Api.Domain.DTOs;

namespace EvCharge.Api.Infrastructure.Mapping
{
    public static class OwnerMapping
    {
        public static OwnerResponse ToResponse(this Owner o)
        {
            return new OwnerResponse
            {
                Nic = o.Nic,
                FullName = o.FullName,
                Email = o.Email,
                Phone = o.Phone,
                Address = o.Address,
                IsActive = o.IsActive,
                Roles = o.Roles ?? new(),
                CreatedAtUtc = o.CreatedAtUtc,
                UpdatedAtUtc = o.UpdatedAtUtc
            };
        }

        public static AdminBackOfficeListItem ToAdminBackOfficeListItem(this Owner o)
        {
            return new AdminBackOfficeListItem
            {
                Nic = o.Nic,
                FullName = o.FullName,
                Email = o.Email,
                Phone = o.Phone,
                Address = o.Address,
                IsActive = o.IsActive,
                Roles = o.Roles ?? new(),

                BackOfficeProfile = o.BackOfficeProfile is null ? null : new AdminBackOfficeProfileDto
                {
                    BusinessName = o.BackOfficeProfile.BusinessName,
                    Brn = o.BackOfficeProfile.Brn,
                    ContactEmail = o.BackOfficeProfile.ContactEmail,
                    ContactPhone = o.BackOfficeProfile.ContactPhone,
                    ApplicationStatus = o.BackOfficeProfile.ApplicationStatus,
                    SubmittedAtUtc = o.BackOfficeProfile.SubmittedAtUtc,
                    ReviewedAtUtc = o.BackOfficeProfile.ReviewedAtUtc,
                    ReviewedByNic = o.BackOfficeProfile.ReviewedByNic,
                    ReviewNotes = o.BackOfficeProfile.ReviewNotes
                },

                CreatedAtUtc = o.CreatedAtUtc,
                UpdatedAtUtc = o.UpdatedAtUtc,
                DeactivatedAtUtc = o.DeactivatedAtUtc,
                DeactivatedBy = o.DeactivatedBy,
                UpdatedBy = o.UpdatedBy
            };
        }
        
         public static AdminFullOwnerDto ToAdminFullOwnerDto(this Owner o, bool includeSensitive = false)
        {
            var dto = new AdminFullOwnerDto
            {
                Id = o.Id,
                Nic = o.Nic,
                FullName = o.FullName,
                Email = o.Email,
                Phone = o.Phone,
                Address = o.Address,
                IsActive = o.IsActive,
                Roles = o.Roles ?? new(),
                BackOfficeNic = o.BackOfficeNic,
                OperatorStationIds = o.OperatorStationIds,
                CreatedAtUtc = o.CreatedAtUtc,
                UpdatedAtUtc = o.UpdatedAtUtc,
                DeactivatedAtUtc = o.DeactivatedAtUtc,
                DeactivatedBy = o.DeactivatedBy,
                UpdatedBy = o.UpdatedBy,
                BackOfficeProfile = o.BackOfficeProfile is null ? null : new AdminBackOfficeProfileDto
                {
                    BusinessName = o.BackOfficeProfile.BusinessName,
                    Brn = o.BackOfficeProfile.Brn,
                    ContactEmail = o.BackOfficeProfile.ContactEmail,
                    ContactPhone = o.BackOfficeProfile.ContactPhone,
                    ApplicationStatus = o.BackOfficeProfile.ApplicationStatus,
                    SubmittedAtUtc = o.BackOfficeProfile.SubmittedAtUtc,
                    ReviewedAtUtc = o.BackOfficeProfile.ReviewedAtUtc,
                    ReviewedByNic = o.BackOfficeProfile.ReviewedByNic,
                    ReviewNotes = o.BackOfficeProfile.ReviewNotes
                }
            };

            if (includeSensitive)
            {
                dto.EmailLower = o.EmailLower;
                dto.PasswordHash = o.PasswordHash;
            }

            return dto;
        }

    }
}
