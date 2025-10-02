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
}
