// using backend.Models;
// using backend.Repositories;
// using Microsoft.AspNetCore.Mvc;

// namespace backend.Controllers
// {
//     [ApiController]
//     [Route("api/[controller]")]
//     public class UserController : ControllerBase
//     {
//         private readonly UserRepository _userRepository;

//         public UserController(UserRepository userRepository)
//         {
//             _userRepository = userRepository;
//         }

//         // ✅ Get all users
//         [HttpGet]
//         public async Task<IActionResult> GetAll()
//         {
//             var users = await _userRepository.GetAllAsync();
//             return Ok(users);
//         }

//         // ✅ Get user by ID
//         [HttpGet("{id}")]
//         public async Task<IActionResult> GetById(string id)
//         {
//             var user = await _userRepository.GetByIdAsync(id);
//             if (user == null)
//                 return NotFound(new { message = "User not found" });
//             return Ok(user);
//         }

//         // ✅ Create a new user
//         [HttpPost]
//         public async Task<IActionResult> Create([FromBody] User newUser)
//         {
//             if (string.IsNullOrEmpty(newUser.Username) || string.IsNullOrEmpty(newUser.Email))
//                 return BadRequest(new { message = "Username and Email are required" });

//             newUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newUser.PasswordHash); // Hash password
//             await _userRepository.CreateAsync(newUser);
//             return CreatedAtAction(nameof(GetById), new { id = newUser.Id }, newUser);
//         }

//         // ✅ Update existing user
//         [HttpPut("{id}")]
//         public async Task<IActionResult> Update(string id, [FromBody] User updatedUser)
//         {
//             var existing = await _userRepository.GetByIdAsync(id);
//             if (existing == null)
//                 return NotFound(new { message = "User not found" });

//             updatedUser.Id = id;
//             if (!string.IsNullOrEmpty(updatedUser.PasswordHash))
//                 updatedUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updatedUser.PasswordHash);

//             await _userRepository.UpdateAsync(id, updatedUser);
//             return Ok(new { message = "User updated successfully" });
//         }

//         // ✅ Delete user
//         [HttpDelete("{id}")]
//         public async Task<IActionResult> Delete(string id)
//         {
//             var existing = await _userRepository.GetByIdAsync(id);
//             if (existing == null)
//                 return NotFound(new { message = "User not found" });

//             await _userRepository.DeleteAsync(id);
//             return Ok(new { message = "User deleted successfully" });
//         }
//     }
// }
using backend.Models;
using backend.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly UserRepository _userRepository;

        public UserController(UserRepository userRepository)
        {
            _userRepository = userRepository;
        }

        // ✅ Get all users
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var users = await _userRepository.GetAllAsync();
            return Ok(users);
        }

        // ✅ Get user by ID
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
                return NotFound(new { message = "User not found" });
            return Ok(user);
        }

        // ✅ Get user by username
        [HttpGet("username/{username}")]
        public async Task<IActionResult> GetByUsername(string username)
        {
            var user = await _userRepository.GetByUsernameAsync(username);
            if (user == null)
                return NotFound(new { message = "User not found" });
            return Ok(user);
        }

        // ✅ Create a new staff user
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] User newUser)
        {
            if (string.IsNullOrWhiteSpace(newUser.Username) ||
                string.IsNullOrWhiteSpace(newUser.Email) ||
                string.IsNullOrWhiteSpace(newUser.PasswordHash) ||
                string.IsNullOrWhiteSpace(newUser.Role))
            {
                return BadRequest(new { message = "Username, Email, Password, and Role are required" });
            }

            newUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newUser.PasswordHash);
            newUser.CreatedAt = DateTime.UtcNow;
            newUser.IsActive = true;

            await _userRepository.CreateAsync(newUser);
            return CreatedAtAction(nameof(GetById), new { id = newUser.Id }, newUser);
        }

        // ✅ Update existing staff user
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] User updatedUser)
        {
            var existing = await _userRepository.GetByIdAsync(id);
            if (existing == null)
                return NotFound(new { message = "User not found" });

            existing.Username = updatedUser.Username ?? existing.Username;
            existing.Email = updatedUser.Email ?? existing.Email;
            existing.Role = updatedUser.Role ?? existing.Role;
            existing.IsActive = updatedUser.IsActive;

            if (!string.IsNullOrWhiteSpace(updatedUser.PasswordHash))
                existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updatedUser.PasswordHash);

            await _userRepository.UpdateAsync(id, existing);
            return Ok(new { message = "User updated successfully" });
        }

        // ✅ Delete staff user
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var existing = await _userRepository.GetByIdAsync(id);
            if (existing == null)
                return NotFound(new { message = "User not found" });

            await _userRepository.DeleteAsync(id);
            return Ok(new { message = "User deleted successfully" });
        }
    }
}
