using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EvCharge.Api.Domain
{
    public class DayTimeRange
    {
        public string Start { get; set; } = "09:00"; // HH:mm
        public string End { get; set; } = "17:00";
    }

    public class WeeklySchedule
    {
        public List<DayTimeRange> Mon { get; set; } = new();
        public List<DayTimeRange> Tue { get; set; } = new();
        public List<DayTimeRange> Wed { get; set; } = new();
        public List<DayTimeRange> Thu { get; set; } = new();
        public List<DayTimeRange> Fri { get; set; } = new();
        public List<DayTimeRange> Sat { get; set; } = new();
        public List<DayTimeRange> Sun { get; set; } = new();
    }

    public class ScheduleException
    {
        public string Date { get; set; } = "";  // yyyy-MM-dd (station local date)
        public bool Closed { get; set; } = false;
    }

    public class CapacityOverride
    {
        public string Date { get; set; } = "";  // yyyy-MM-dd
        public int Connectors { get; set; } = 0;
    }

    public class StationSchedule
    {
        [BsonId, BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        [BsonRepresentation(BsonType.ObjectId)]
        public string StationId { get; set; } = string.Empty;

        public WeeklySchedule Weekly { get; set; } = new WeeklySchedule();
        public List<ScheduleException> Exceptions { get; set; } = new();
        public List<CapacityOverride> CapacityOverrides { get; set; } = new();
        public DateTime UpdatedAtUtc { get; set; }
    }
}
