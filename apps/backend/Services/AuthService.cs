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
        // NEW unified login
        Task<AuthLoginResponse> LoginAsync(LoginRequest req, CancellationToken ct);

        // Legacy owner-only login (kept for backward compatibility)
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

        // -----------------------
        // Unified login (NIC or Email)
        // -----------------------
        public async Task<AuthLoginResponse> LoginAsync(LoginRequest req, CancellationToken ct)
        {
            if (string.IsNullOrWhiteSpace(req.Username))
                throw new AuthException("InvalidUsername", "Username is required.");

            var username = req.Username.Trim();
            var byEmail = EmailValidator.IsValid(username);
            var byNic = !byEmail && NicValidator.IsValid(username);

            if (!byEmail && !byNic)
                throw new AuthException("InvalidUsername", "Username must be a valid NIC or a valid email.");

            var account = byEmail
                ? await _owners.GetByEmailLowerAsync(username.ToLowerInvariant(), ct)
                : await _owners.GetByNicAsync(username, ct);

            if (account is null)
                throw new AuthException("InvalidCredentials", "Username or password is incorrect.");
            if (!account.IsActive)
                throw new AuthException("AccountDisabled", "Your account is deactivated.");

            var ok = BCrypt.Net.BCrypt.Verify(req.Password ?? string.Empty, account.PasswordHash);
            if (!ok)
                throw new AuthException("InvalidCredentials", "Username or password is incorrect.");

            var claims = BuildClaims(account);

            var token = _jwt.CreateToken(claims);
            var expires = DateTime.UtcNow.AddMinutes(_jwtOpts.ExpiryMinutes);

            return new AuthLoginResponse
            {
                AccessToken = token,
                ExpiresAtUtc = expires,
                Nic = account.Nic,
                FullName = account.FullName,
                Roles = account.Roles ?? new(),
                Email = account.Email ?? string.Empty,
                OperatorStationIds = account.OperatorStationIds ?? new()
            };
        }

        // -----------------------
        // Legacy owner-only login
        // -----------------------
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

            var claims = BuildClaims(owner);

            var token = _jwt.CreateToken(claims);
            var expires = DateTime.UtcNow.AddMinutes(_jwtOpts.ExpiryMinutes);

            return new AuthLoginResponse
            {
                AccessToken = token,
                ExpiresAtUtc = expires,
                Nic = owner.Nic,
                FullName = owner.FullName,
                Roles = owner.Roles ?? new(),
                Email = owner.Email ?? string.Empty,
                OperatorStationIds = owner.OperatorStationIds ?? new()
            };
        }

        // Build common claims for JWT
        private static List<Claim> BuildClaims(EvCharge.Api.Domain.Owner account)
        {
            var claims = new List<Claim>
            {
                // Keep NIC as subject to remain compatible with downstream code (e.g., BookingController uses sub as NIC)
                new Claim(JwtRegisteredClaimNames.Sub, account.Nic),
                new Claim(JwtRegisteredClaimNames.UniqueName, account.Nic),
                new Claim(ClaimTypes.Name, account.FullName ?? string.Empty)
            };

            if (account.Roles is not null)
            {
                foreach (var r in account.Roles)
                    if (!string.IsNullOrWhiteSpace(r))
                        claims.Add(new Claim(ClaimTypes.Role, r));
            }

            if (account.OperatorStationIds is not null)
            {
                foreach (var sid in account.OperatorStationIds)
                    if (!string.IsNullOrWhiteSpace(sid))
                        claims.Add(new Claim("OperatorStationId", sid)); // custom multi-valued claim
            }

            return claims;
        }
    }
}
