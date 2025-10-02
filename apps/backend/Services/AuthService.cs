using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using EvCharge.Api.Domain.DTOs;
using EvCharge.Api.Infrastructure;
using EvCharge.Api.Infrastructure.Errors;
using EvCharge.Api.Infrastructure.Validation;
using EvCharge.Api.Options;
using EvCharge.Api.Repositories;
using Microsoft.Extensions.Options;

namespace EvCharge.Api.Services
{
    public interface IAuthService
    {
        Task<AuthLoginResponse> LoginOwnerAsync(LoginOwnerRequest req, CancellationToken ct);
    }

    public class AuthService : IAuthService
    {
        private readonly IEvOwnerRepository _owners;
        private readonly IJwtTokenService _jwt;
        private readonly JwtOptions _jwtOpts;

        public AuthService(IEvOwnerRepository owners, IJwtTokenService jwt, IOptions<JwtOptions> jwtOptions)
        {
            _owners = owners; _jwt = jwt; _jwtOpts = jwtOptions.Value;
        }

        public async Task<AuthLoginResponse> LoginOwnerAsync(LoginOwnerRequest req, CancellationToken ct)
        {
            if (!NicValidator.IsValid(req.Nic))
                throw new AuthException("InvalidNic", "NIC format is invalid.");

            var owner = await _owners.GetByNicAsync(req.Nic.Trim(), ct);
            if (owner is null)
                throw new AuthException("InvalidCredentials", "NIC or password is incorrect.");
            if (!owner.IsActive)
                throw new AuthException("AccountDisabled", "Your account is deactivated.");

            var ok = BCrypt.Net.BCrypt.Verify(req.Password ?? string.Empty, owner.PasswordHash);
            if (!ok)
                throw new AuthException("InvalidCredentials", "NIC or password is incorrect.");

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, owner.Nic),
                new Claim(JwtRegisteredClaimNames.UniqueName, owner.Nic),
                new Claim(ClaimTypes.Name, owner.FullName)
            };
            if (owner.Roles is not null)
                foreach (var r in owner.Roles) claims.Add(new Claim(ClaimTypes.Role, r));

            var token = _jwt.CreateToken(claims);
            var expires = DateTime.UtcNow.AddMinutes(_jwtOpts.ExpiryMinutes);

            return new AuthLoginResponse
            {
                AccessToken = token,
                ExpiresAtUtc = expires,
                Nic = owner.Nic,
                FullName = owner.FullName,
                Roles = owner.Roles ?? new()
            };
        }
    }
}
