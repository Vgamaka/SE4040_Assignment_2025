namespace EvCharge.Api.Domain.DTOs
{
    public class BackOfficeApplyRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Email    { get; set; } = string.Empty;
        public string? Phone   { get; set; }
        public string Password { get; set; } = string.Empty;

        // business profile
        public string BusinessName { get; set; } = string.Empty;
        public string? ContactEmail { get; set; }
        public string? ContactPhone { get; set; }
    }

    public class OperatorCreateRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string Email    { get; set; } = string.Empty;
        public string? Phone   { get; set; }
        public string Password { get; set; } = string.Empty;

        // optional initial scoping
        public List<string>? StationIds { get; set; }
    }

    public class OperatorAttachStationsRequest
    {
        public List<string> StationIds { get; set; } = new();
    }
}
