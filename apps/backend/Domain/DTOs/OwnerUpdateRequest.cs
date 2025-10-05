namespace EvCharge.Api.Domain.DTOs
{
    public class OwnerUpdateRequest
    {
        // All optional; only provided fields will be updated.
        public string? FullName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }

        public string? AddressLine1 { get; set; }
        public string? AddressLine2 { get; set; }
        public string? City { get; set; }
    }
}
