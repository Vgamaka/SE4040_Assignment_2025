using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EvCharge.Api.Domain
{
    public class BackOfficeProfile
    {
        [BsonElement("businessName")]
        public string BusinessName { get; set; } = string.Empty;

        [BsonElement("brn")]
        public string? Brn { get; set; }

        // NEW: contact details used by BackOfficeService
        [BsonElement("contactEmail")]
        public string ContactEmail { get; set; } = string.Empty;

        [BsonElement("contactPhone")]
        public string? ContactPhone { get; set; }

        [BsonElement("applicationStatus")] // Pending|Approved|Rejected
        public string ApplicationStatus { get; set; } = "Pending";

        [BsonElement("submittedAtUtc")]
        public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;

        [BsonElement("reviewedAtUtc")]
        public DateTime? ReviewedAtUtc { get; set; }

        [BsonElement("reviewedByNic")]
        public string? ReviewedByNic { get; set; }

        [BsonElement("reviewNotes")]
        public string? ReviewNotes { get; set; }
    }

    public class Owner
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        // Natural PK for our domain (unique index in Mongo)
        [BsonElement("nic")]
        public string Nic { get; set; } = string.Empty;

        [BsonElement("fullName")]
        public string FullName { get; set; } = string.Empty;

        [BsonElement("email")]
        public string Email { get; set; } = string.Empty;

        // for uniqueness (case-insensitive)
        [BsonElement("emailLower")]
        public string EmailLower { get; set; } = string.Empty;

        [BsonElement("phone")]
        public string? Phone { get; set; }

        [BsonElement("passwordHash")]
        public string PasswordHash { get; set; } = string.Empty;

        [BsonElement("address")]
        public Address? Address { get; set; }

        [BsonElement("isActive")]
        public bool IsActive { get; set; } = true;

        [BsonElement("roles")]
        public List<string> Roles { get; set; } = new() { "Owner" };

        // ----- BackOffice account (business application) -----
        [BsonElement("backOfficeProfile")]
        public BackOfficeProfile? BackOfficeProfile { get; set; }

        // ----- Operator account linking & scoping -----
        [BsonElement("backOfficeNic")]
        public string? BackOfficeNic { get; set; }  // parent BackOffice NIC (for Operators)

        [BsonElement("operatorStationIds")]
        public List<string>? OperatorStationIds { get; set; }  // stations the Operator can manage

        [BsonElement("createdAtUtc")]
        public DateTime CreatedAtUtc { get; set; }

        [BsonElement("updatedAtUtc")]
        public DateTime? UpdatedAtUtc { get; set; }

        [BsonElement("deactivatedAtUtc")]
        public DateTime? DeactivatedAtUtc { get; set; }

        [BsonElement("deactivatedBy")]
        public string? DeactivatedBy { get; set; }

        [BsonElement("updatedBy")]
        public string? UpdatedBy { get; set; }
    }
}
