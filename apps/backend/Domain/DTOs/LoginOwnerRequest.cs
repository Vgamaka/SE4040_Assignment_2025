namespace EvCharge.Api.Domain.DTOs
{
    public class LoginOwnerRequest
    {
        public string Nic { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
