using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace backend.Models
{
    [BsonIgnoreExtraElements] // 👈 ignore fields not in this class (e.g., latitude)
    public class ChargingStation
    {
        [BsonId]
        [BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonElement("name")]
        public string? Name { get; set; }

        [BsonElement("location")]
        public string? Location { get; set; }  // human-readable address/area

        [BsonElement("type")]
        public string? Type { get; set; }      // "AC" or "DC"

        [BsonElement("availableSlots")]
        public int AvailableSlots { get; set; }

        [BsonElement("schedule")]
        public List<StationSchedule> Schedule { get; set; } = new();

        [BsonElement("isActive")]
        public bool IsActive { get; set; } = true;
    }

    [BsonIgnoreExtraElements] // 👈 also safe for schedule docs
    public class StationSchedule
    {
        [BsonElement("dayOfWeek")]
        public string? DayOfWeek { get; set; }

        [BsonElement("openTime")]
        public string? OpenTime { get; set; }

        [BsonElement("closeTime")]
        public string? CloseTime { get; set; }
    }
}
