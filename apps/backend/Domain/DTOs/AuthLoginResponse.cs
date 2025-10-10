namespace EvCharge.Api.Domain.DTOs
{
    public class AuthLoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string TokenType { get; set; } = "Bearer";
        public DateTime ExpiresAtUtc { get; set; }

        // minimal subject info for client
        public string Nic { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public List<string> Roles { get; set; } = new();

        // new (non-breaking additions for unified auth)
        public string Email { get; set; } = string.Empty;
        public List<string> OperatorStationIds { get; set; } = new();
    }
}
