using System;

namespace EvCharge.Api.Domain.DTOs
{
    /// <summary>
    /// Unified login request. Owners use NIC as username; staff use email.
    /// </summary>
    public class LoginRequest
    {
        public string Username { get; set; } = string.Empty; // NIC or email
        public string Password { get; set; } = string.Empty;
    }
}
