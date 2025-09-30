using backend.Models;
using backend.Repositories;

namespace backend.Services
{
    /// <summary>
    /// Encapsulates auth-related operations (login and optional registration helpers).
    /// Controllers can delegate to this to keep actions thin.
    /// </summary>
    public class AuthService
    {
        private readonly UserRepository _userRepo;
        private readonly EvOwnerRepository _ownerRepo;
        private readonly JwtTokenService _jwt;

        public AuthService(UserRepository userRepo, EvOwnerRepository ownerRepo, JwtTokenService jwt)
        {
            _userRepo = userRepo;
            _ownerRepo = ownerRepo;
            _jwt = jwt;
        }

        /// <summary>
        /// Staff login (Backoffice / StationOperator) using username + password.
        /// Returns a LoginResponse with JWT and role.
        /// </summary>
        public async Task<LoginResponse> LoginStaffAsync(LoginRequest req)
        {
            if (req is null || string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                throw new ArgumentException("Username and password are required");

            var user = await _userRepo.GetByUsernameAsync(req.Username);
            if (user == null || !user.IsActive || string.IsNullOrWhiteSpace(user.PasswordHash))
                throw new UnauthorizedAccessException("Invalid credentials");

            var ok = BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash);
            if (!ok) throw new UnauthorizedAccessException("Invalid credentials");

            var token = _jwt.GenerateTokenForUser(user);
            return new LoginResponse
            {
                Token = token,
                Role = user.Role ?? "StationOperator",
                Username = user.Username ?? string.Empty
            };
        }

        /// <summary>
        /// EV Owner login using NIC + password.
        /// Returns a LoginResponse with JWT and role EvOwner.
        /// </summary>
        public async Task<LoginResponse> LoginOwnerAsync(LoginRequest req)
        {
            if (req is null || string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                throw new ArgumentException("NIC and password are required");

            var nic = req.Username;
            var owner = await _ownerRepo.GetByNICAsync(nic);
            if (owner == null || !owner.IsActive || string.IsNullOrWhiteSpace(owner.PasswordHash))
                throw new UnauthorizedAccessException("Invalid credentials");

            var ok = BCrypt.Net.BCrypt.Verify(req.Password, owner.PasswordHash);
            if (!ok) throw new UnauthorizedAccessException("Invalid credentials");

            var token = _jwt.GenerateTokenForOwner(owner);
            return new LoginResponse
            {
                Token = token,
                Role = "EvOwner",
                Username = owner.NIC ?? string.Empty
            };
        }

        /// <summary>
        /// Optional helper to register a staff user from code (admins or seeders).
        /// Your UserController already exposes POST /api/User, so this is just a utility.
        /// </summary>
        public async Task<User> RegisterStaffAsync(User newUser)
        {
            if (string.IsNullOrWhiteSpace(newUser.Username) ||
                string.IsNullOrWhiteSpace(newUser.Email) ||
                string.IsNullOrWhiteSpace(newUser.PasswordHash) ||
                string.IsNullOrWhiteSpace(newUser.Role))
            {
                throw new ArgumentException("Username, Email, Password, and Role are required");
            }

            newUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newUser.PasswordHash);
            newUser.CreatedAt = DateTime.UtcNow;
            newUser.IsActive = true;

            await _userRepo.CreateAsync(newUser);
            return newUser;
        }
    }
}
