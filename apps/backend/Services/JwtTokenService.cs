// using System.IdentityModel.Tokens.Jwt;
// using System.Security.Claims;
// using System.Text;
// using Microsoft.IdentityModel.Tokens;
// using backend.Models;

// namespace backend.Services
// {
//     public class JwtTokenService
//     {
//         private readonly IConfiguration _config;

//         public JwtTokenService(IConfiguration config)
//         {
//             _config = config;
//         }

//         public string GenerateTokenForUser(User user)
//         {
//             // Backoffice / StationOperator
//             var claims = new List<Claim>
//             {
//                 new Claim(JwtRegisteredClaimNames.Sub, user.Id ?? string.Empty),
//                 new Claim(JwtRegisteredClaimNames.UniqueName, user.Username ?? string.Empty),
//                 new Claim(ClaimTypes.Role, user.Role ?? "StationOperator")
//             };

//             return BuildToken(claims);
//         }

//         public string GenerateTokenForOwner(EvOwner owner)
//         {
//             // EvOwner always role "EvOwner"
//             var claims = new List<Claim>
//             {
//                 new Claim(JwtRegisteredClaimNames.Sub, owner.NIC ?? string.Empty),
//                 new Claim(JwtRegisteredClaimNames.UniqueName, owner.Email ?? owner.NIC ?? string.Empty),
//                 new Claim(ClaimTypes.Role, "EvOwner")
//             };

//             return BuildToken(claims);
//         }

//         private string BuildToken(List<Claim> claims)
//         {
//             var issuer = _config["Jwt:Issuer"]!;
//             var audience = _config["Jwt:Audience"]!;
//             var secret = _config["Jwt:Secret"]!;
//             var expiryMinutes = int.TryParse(_config["Jwt:ExpiryMinutes"], out var m) ? m : 60;

//             var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
//             var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

//             var token = new JwtSecurityToken(
//                 issuer: issuer,
//                 audience: audience,
//                 claims: claims,
//                 notBefore: DateTime.UtcNow,
//                 expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
//                 signingCredentials: creds
//             );

//             return new JwtSecurityTokenHandler().WriteToken(token);
//         }
//     }
// }
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using backend.Models;

namespace backend.Services
{
    public class JwtTokenService
    {
        private readonly IConfiguration _config;
        private readonly SymmetricSecurityKey _key;
        private readonly int _expiryMinutes;

        public JwtTokenService(IConfiguration config)
        {
            _config = config;
            var secret = _config["Jwt:Secret"] ?? throw new Exception("Jwt:Secret not configured");
            _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));

            // Default expiry = 7 days if config missing
            if (int.TryParse(_config["Jwt:ExpiryMinutes"], out var mins))
                _expiryMinutes = mins;
            else
                _expiryMinutes = 60 * 24 * 7;
        }

        public string GenerateTokenForUser(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.Username ?? string.Empty),
                new Claim("role", user.Role ?? "StationOperator")
            };

            return BuildToken(claims);
        }

        public string GenerateTokenForOwner(EvOwner owner)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, owner.NIC ?? string.Empty),
                new Claim(JwtRegisteredClaimNames.UniqueName, owner.Email ?? owner.NIC ?? string.Empty),
                new Claim("role", "EvOwner"),
                new Claim("nic", owner.NIC ?? string.Empty)
            };

            return BuildToken(claims);
        }

        private string BuildToken(List<Claim> claims)
        {
            var issuer = _config["Jwt:Issuer"] ?? "default-issuer";
            var audience = _config["Jwt:Audience"] ?? "default-audience";

            var creds = new SigningCredentials(_key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(_expiryMinutes),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // === Extra helpers for QR checksum ===

        public byte[] HmacBytes(string data)
        {
            using var hmac = new HMACSHA256(_key.Key);
            return hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
        }

        public string HmacBase64Url(string data)
        {
            var hash = HmacBytes(data);
            return Base64UrlEncoder.Encode(hash);
        }
    }
}
