using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace backend.Models
{
    /// <summary>
    /// Represents an EV Owner who uses the mobile app.
    /// </summary>
    public class EvOwner
    {
        [BsonId]
        [BsonRepresentation(BsonType.String)]
        public string? NIC { get; set; }  // ✅ Nullable so ASP.NET won't require it at model binding time

        [BsonElement("name")]
        public string? Name { get; set; }

        [BsonElement("email")]
        public string? Email { get; set; }

        [BsonElement("phone")]
        public string? Phone { get; set; }

        [BsonElement("vehicleNumber")]
        public string? VehicleNumber { get; set; }

        [BsonElement("passwordHash")]
        public string? PasswordHash { get; set; }

        [BsonElement("createdAt")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [BsonElement("isActive")]
        public bool IsActive { get; set; } = true;
    }
}
