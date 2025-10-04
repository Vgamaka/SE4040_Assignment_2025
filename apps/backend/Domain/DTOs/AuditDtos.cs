namespace EvCharge.Api.Domain.DTOs
{
    public class AuditSearchRequest
    {
        public string? EntityType { get; set; }   // "booking" | "session"
        public string? EntityId { get; set; }     // ObjectId string
        public string? Action { get; set; }       // Approved|Rejected|...
        public string? Actor { get; set; }        // actorNic contains
        public DateTime? FromUtc { get; set; }
        public DateTime? ToUtc { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 50;
    }

    public class AuditItemDto
    {
        public string Id { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public string EntityId { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string? ActorNic { get; set; }
        public string? ActorRole { get; set; }
        public object? Payload { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class PagedResponse<T>
    {
        public int Page { get; set; }
        public int PageSize { get; set; }
        public long Total { get; set; }
        public List<T> Items { get; set; } = new();
    }
}
