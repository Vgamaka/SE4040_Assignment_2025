// using MongoDB.Bson;
// using MongoDB.Bson.Serialization.Attributes;

// namespace backend.Models
// {
//     /// <summary>
//     /// Represents an EV charging station.
//     /// </summary>
//     public class ChargingStation
//     {
//         [BsonId]
//         [BsonRepresentation(BsonType.ObjectId)]
//         public string? Id { get; set; }   // ✅ Nullable → MongoDB generates ID

//         [BsonElement("name")]
//         public string? Name { get; set; }

//         [BsonElement("location")]
//         public string? Location { get; set; }  // e.g. GPS address or description

//         [BsonElement("type")]
//         public string? Type { get; set; }  // e.g. "AC" or "DC"

//         [BsonElement("availableSlots")]
//         public int AvailableSlots { get; set; }

//         [BsonElement("schedule")]
//         public List<StationSchedule> Schedule { get; set; } = new List<StationSchedule>();

//         [BsonElement("isActive")]
//         public bool IsActive { get; set; } = true;
//     }

//     /// <summary>
//     /// Represents the availability schedule for a charging station.
//     /// </summary>
//     public class StationSchedule
//     {
//         [BsonElement("dayOfWeek")]
//         public string? DayOfWeek { get; set; }  // e.g. "Monday"

//         [BsonElement("openTime")]
//         public string? OpenTime { get; set; }   // e.g. "08:00"

//         [BsonElement("closeTime")]
//         public string? CloseTime { get; set; }  // e.g. "20:00"
//     }
// }

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace backend.Models
{
    /// <summary>
    /// Represents an EV charging station.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class ChargingStation
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }   // ✅ Nullable → MongoDB generates ID

        [BsonElement("name")]
        public string? Name { get; set; }

        [BsonElement("location")]
        public string? Location { get; set; }  // e.g. GPS address or description

        [BsonElement("type")]
        public string? Type { get; set; }  // e.g. "AC" or "DC"

        [BsonElement("availableSlots")]
        public int AvailableSlots { get; set; }

        [BsonElement("schedule")]
        public List<StationSchedule> Schedule { get; set; } = new List<StationSchedule>();

        [BsonElement("isActive")]
        public bool IsActive { get; set; } = true;

        // === NEW: Coordinates support for map ===
        [BsonElement("latitude")]
        public double? Latitude { get; set; }   // optional, can be null if not set

        [BsonElement("longitude")]
        public double? Longitude { get; set; }  // optional, can be null if not set
    }

    /// <summary>
    /// Represents the availability schedule for a charging station.
    /// </summary>
    public class StationSchedule
    {
        [BsonElement("dayOfWeek")]
        public string? DayOfWeek { get; set; }  // e.g. "Monday"

        [BsonElement("openTime")]
        public string? OpenTime { get; set; }   // e.g. "08:00"

        [BsonElement("closeTime")]
        public string? CloseTime { get; set; }  // e.g. "20:00"
    }
}
