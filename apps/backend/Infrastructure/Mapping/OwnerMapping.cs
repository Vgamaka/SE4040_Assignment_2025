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
    }
}
