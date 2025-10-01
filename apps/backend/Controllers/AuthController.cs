// using Microsoft.AspNetCore.Authorization;
// using Microsoft.AspNetCore.Mvc;
// using backend.Repositories;
// using backend.Models;
// using backend.Services;

// namespace backend.Controllers
// {
//     [ApiController]
//     [Route("api/[controller]")]
//     [Authorize] // default: protect controller; overridden by [AllowAnonymous] on login endpoints
//     public class AuthController : ControllerBase
//     {
//         private readonly UserRepository _userRepo;
//         private readonly EvOwnerRepository _ownerRepo;
//         private readonly JwtTokenService _jwt;

//         public AuthController(UserRepository userRepo, EvOwnerRepository ownerRepo, JwtTokenService jwt)
//         {
//             _userRepo = userRepo;
//             _ownerRepo = ownerRepo;
//             _jwt = jwt;
//         }

//         // POST: /api/auth/login  (Backoffice / StationOperator)
//         [AllowAnonymous]
//         [HttpPost("login")]
//         public async Task<IActionResult> Login([FromBody] LoginRequest req)
//         {
//             if (req is null || string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
//                 return BadRequest(new { message = "Username and password are required" });

//             var user = await _userRepo.GetByUsernameAsync(req.Username);
//             if (user == null || !user.IsActive)
//                 return Unauthorized(new { message = "Invalid credentials" });

//             var ok = BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash);
//             if (!ok) return Unauthorized(new { message = "Invalid credentials" });

//             var token = _jwt.GenerateTokenForUser(user);
//             return Ok(new LoginResponse { Token = token, Role = user.Role ?? "", Username = user.Username ?? "" });
//         }

//         // POST: /api/auth/owner/login  (EvOwner)
//         [AllowAnonymous]
//         [HttpPost("owner/login")]
//         public async Task<IActionResult> OwnerLogin([FromBody] LoginRequest req)
//         {
//             if (req is null || string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
//                 return BadRequest(new { message = "NIC and password are required" });

//             var nic = req.Username; // spec: owners log in with NIC
//             var owner = await _ownerRepo.GetByNICAsync(nic);
//             if (owner == null || !owner.IsActive)
//                 return Unauthorized(new { message = "Invalid credentials" });

//             var ok = BCrypt.Net.BCrypt.Verify(req.Password, owner.PasswordHash);
//             if (!ok) return Unauthorized(new { message = "Invalid credentials" });

//             var token = _jwt.GenerateTokenForOwner(owner);
//             return Ok(new LoginResponse { Token = token, Role = "EvOwner", Username = owner.NIC ?? "" });
//         }
//     }
// }
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using backend.Repositories;
using backend.Models;
using backend.Services;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize] // default: protect controller; overridden by [AllowAnonymous] on login endpoints
    public class AuthController : ControllerBase
    {
        private readonly UserRepository _userRepo;
        private readonly EvOwnerRepository _ownerRepo;
        private readonly JwtTokenService _jwt;

        public AuthController(UserRepository userRepo, EvOwnerRepository ownerRepo, JwtTokenService jwt)
        {
            _userRepo = userRepo;
            _ownerRepo = ownerRepo;
            _jwt = jwt;
        }

        // POST: /api/Auth/login  (Backoffice / StationOperator)
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (req is null || string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "Username and password are required" });

            var user = await _userRepo.GetByUsernameAsync(req.Username);
            if (user == null || !user.IsActive)
                return Unauthorized(new { message = "Invalid credentials" });

            var ok = BCrypt.Net.BCrypt.Verify(req.Password, user.PasswordHash);
            if (!ok) return Unauthorized(new { message = "Invalid credentials" });

            var token = _jwt.GenerateTokenForUser(user);

            return Ok(new LoginResponse
            {
                Token = token,
                Role = user.Role ?? "StationOperator",
                Username = user.Username ?? ""
            });
        }

        // POST: /api/Auth/owner/login  (EvOwner → login with NIC)
        [AllowAnonymous]
        [HttpPost("owner/login")]
        public async Task<IActionResult> OwnerLogin([FromBody] LoginRequest req)
        {
            if (req is null || string.IsNullOrWhiteSpace(req.Username) || string.IsNullOrWhiteSpace(req.Password))
                return BadRequest(new { message = "NIC and password are required" });

            var nic = req.Username; // spec: owners log in with NIC
            var owner = await _ownerRepo.GetByNICAsync(nic);
            if (owner == null || !owner.IsActive)
                return Unauthorized(new { message = "Invalid credentials" });

            var ok = BCrypt.Net.BCrypt.Verify(req.Password, owner.PasswordHash);
            if (!ok) return Unauthorized(new { message = "Invalid credentials" });

            var token = _jwt.GenerateTokenForOwner(owner);

            return Ok(new LoginResponse
            {
                Token = token,
                Role = "EvOwner",
                Username = owner.NIC ?? ""
            });
        }
    }

    // === Supporting DTOs ===
    public class LoginRequest
    {
        public string? Username { get; set; } // Username for staff, NIC for owners
        public string? Password { get; set; }
    }

    public class LoginResponse
    {
        public string Token { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
    }
}
