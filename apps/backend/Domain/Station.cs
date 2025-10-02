using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EvCharge.Api.Domain
{
    public class GeoPoint
    {
        [BsonElement("type")]
        public string Type { get; set; } = "Point";

        [BsonElement("coordinates")]
        public double[] Coordinates { get; set; } = Array.Empty<double>(); // [lng, lat]
    }

    public class Pricing
    {
        public string Model { get; set; } = "flat"; // flat|hourly|kwh
        public decimal Base { get; set; } = 0;
        public decimal PerHour { get; set; } = 0;
        public decimal PerKwh { get; set; } = 0;
        public decimal TaxPct { get; set; } = 0;
    }

    public class Station
    {
        [BsonId, BsonRepresentation(BsonType.ObjectId)]
        public string? Id { get; set; }

        public string Name { get; set; } = string.Empty;             // 2..120
        public string Type { get; set; } = "AC";                      // AC|DC
        public int Connectors { get; set; } = 1;                      // >=1
        public string Status { get; set; } = "Active";                // Active|Inactive|Maintenance
        public bool AutoApproveEnabled { get; set; } = false;         // NEW

        public GeoPoint Location { get; set; } = new GeoPoint         // 2dsphere [lng,lat]
        {
            Type = "Point",
            Coordinates = new double[] { 0, 0 }
        };

        public int DefaultSlotMinutes { get; set; } = 60;             // 30,45,60,90,120
        public Pricing Pricing { get; set; } = new Pricing();

        // timezone string for schedule computations (IANA preferred; Windows works too)
        public string HoursTimezone { get; set; } = "Asia/Colombo";

        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public string? UpdatedBy { get; set; }
    }
}
