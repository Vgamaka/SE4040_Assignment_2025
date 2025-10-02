using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EvCharge.Api.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EvCharge.Api.Infrastructure
{
    public interface IJwtTokenService
    {
        string CreateToken(IEnumerable<Claim> claims);
    }

    public class JwtTokenService : IJwtTokenService
    {
        private readonly JwtOptions _opts;
        private readonly SigningCredentials _creds;

        public JwtTokenService(IOptions<JwtOptions> options)
        {
            _opts = options.Value;
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opts.Secret));
            _creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        }

        public string CreateToken(IEnumerable<Claim> claims)
        {
            var token = new JwtSecurityToken(
                issuer: _opts.Issuer,
                audience: _opts.Audience,
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddMinutes(_opts.ExpiryMinutes),
                signingCredentials: _creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
