using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using backend.Models;

namespace backend.Services
{
    public class JwtTokenService
    {
        private readonly IConfiguration _config;

        public JwtTokenService(IConfiguration config)
        {
            _config = config;
        }

        public string GenerateTokenForUser(User user)
        {
            // Backoffice / StationOperator
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username ?? string.Empty),
                new Claim(ClaimTypes.Role, user.Role ?? "StationOperator")
            };

            return BuildToken(claims);
        }

        public string GenerateTokenForOwner(EvOwner owner)
        {
            // EvOwner always role "EvOwner"
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, owner.NIC ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.UniqueName, owner.Email ?? owner.NIC ?? string.Empty),
                new Claim(ClaimTypes.Role, "EvOwner")
            };

            return BuildToken(claims);
        }

        private string BuildToken(List<Claim> claims)
        {
            var issuer = _config["Jwt:Issuer"]!;
            var audience = _config["Jwt:Audience"]!;
            var secret = _config["Jwt:Secret"]!;
            var expiryMinutes = int.TryParse(_config["Jwt:ExpiryMinutes"], out var m) ? m : 60;

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
