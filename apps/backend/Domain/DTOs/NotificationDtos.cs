namespace EvCharge.Api.Domain.DTOs
{
    public class NotificationListItem
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string Subject { get; set; } = "";
        public string Message { get; set; } = "";
        public Dictionary<string, object?>? Payload { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? ReadAtUtc { get; set; }
    }
}
