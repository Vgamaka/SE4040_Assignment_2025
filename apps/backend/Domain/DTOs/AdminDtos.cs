namespace EvCharge.Api.Domain.DTOs
{
    public class AdminCreateRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Email    { get; set; } = string.Empty;
        public string? Phone   { get; set; }
        public string Password { get; set; } = string.Empty;
    }

    public class BackOfficeReviewRequest
    {
        public string? Notes { get; set; }
    }
    public class AdminBackOfficeProfileDto
    {
        public string BusinessName { get; set; } = string.Empty;
        public string? Brn { get; set; }
        public string ContactEmail { get; set; } = string.Empty;
        public string? ContactPhone { get; set; }
        public string ApplicationStatus { get; set; } = "Pending"; // Pending|Approved|Rejected
        public DateTime SubmittedAtUtc { get; set; }
        public DateTime? ReviewedAtUtc { get; set; }
        public string? ReviewedByNic { get; set; }
        public string? ReviewNotes { get; set; }
    }

    public class AdminBackOfficeListItem
    {
        // Core identity
        public string Nic { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }

        // Optional profile/address
        public Address? Address { get; set; }

        // Status & roles
        public bool IsActive { get; set; }
        public List<string> Roles { get; set; } = new();

        // BackOffice application block
        public AdminBackOfficeProfileDto? BackOfficeProfile { get; set; }

        // Audit fields useful for Admin screen
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public DateTime? DeactivatedAtUtc { get; set; }
        public string? DeactivatedBy { get; set; }
        public string? UpdatedBy { get; set; }
    }
     public class AdminFullOwnerDto
    {
        public string? Id { get; set; }                 // Mongo _id
        public string Nic { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Phone { get; set; }
        public Address? Address { get; set; }
        public bool IsActive { get; set; }
        public List<string> Roles { get; set; } = new();

        // BackOffice application (if any)
        public AdminBackOfficeProfileDto? BackOfficeProfile { get; set; }

        // Operator linkage (if any)
        public string? BackOfficeNic { get; set; }
        public List<string>? OperatorStationIds { get; set; }

        // Audit
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public DateTime? DeactivatedAtUtc { get; set; }
        public string? DeactivatedBy { get; set; }
        public string? UpdatedBy { get; set; }

        // Sensitive (SuperAdmin + includeSensitive=true only)
        public string? EmailLower { get; set; }
        public string? PasswordHash { get; set; }
    }

    public class GeoPointDto
    {
        public string Type { get; set; } = "Point";
        public double[] Coordinates { get; set; } = Array.Empty<double>(); // [lng, lat]
    }

    public class PricingAdminDto
    {
        public string Model { get; set; } = "flat";
        public decimal Base { get; set; }
        public decimal PerHour { get; set; }
        public decimal PerKwh { get; set; }
        public decimal TaxPct { get; set; }
    }

    public class AdminFullStationDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = "AC";
        public int Connectors { get; set; }
        public string Status { get; set; } = "Active";
        public bool AutoApproveEnabled { get; set; }
        public string? BackOfficeNic { get; set; }

        public GeoPointDto Location { get; set; } = new();
        public int DefaultSlotMinutes { get; set; }
        public PricingAdminDto Pricing { get; set; } = new();
        public string HoursTimezone { get; set; } = "Asia/Colombo";

        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
