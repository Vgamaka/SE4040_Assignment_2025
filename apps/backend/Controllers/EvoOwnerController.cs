// using backend.Models;
// using backend.Repositories;
// using Microsoft.AspNetCore.Mvc;
// using BCrypt.Net;

// namespace backend.Controllers
// {
//     [ApiController]
//     [Route("api/[controller]")]
//     public class EvOwnerController : ControllerBase
//     {
//         private readonly EvOwnerRepository _evOwnerRepository;

//         public EvOwnerController(EvOwnerRepository evOwnerRepository)
//         {
//             _evOwnerRepository = evOwnerRepository;
//         }

//         // ✅ Get all EV Owners
//         [HttpGet]
//         public async Task<IActionResult> GetAll()
//         {
//             var owners = await _evOwnerRepository.GetAllAsync();
//             return Ok(owners);
//         }

//         // ✅ Get EV Owner by NIC
//         [HttpGet("{nic}")]
//         public async Task<IActionResult> GetByNIC(string nic)
//         {
//             var owner = await _evOwnerRepository.GetByNICAsync(nic);
//             if (owner == null)
//                 return NotFound(new { message = "EV Owner not found" });
//             return Ok(owner);
//         }

//         // ✅ Create new EV Owner
//         [HttpPost]
//         public async Task<IActionResult> Create([FromBody] EvOwner newOwner)
//         {
//             if (string.IsNullOrWhiteSpace(newOwner.NIC) ||
//                 string.IsNullOrWhiteSpace(newOwner.Email) ||
//                 string.IsNullOrWhiteSpace(newOwner.PasswordHash))
//             {
//                 return BadRequest(new { message = "NIC, Email, and Password are required" });
//             }

//             var existing = await _evOwnerRepository.GetByNICAsync(newOwner.NIC);
//             if (existing != null)
//                 return Conflict(new { message = "An EV Owner with this NIC already exists" });

//             newOwner.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newOwner.PasswordHash);
//             await _evOwnerRepository.CreateAsync(newOwner);
//             return CreatedAtAction(nameof(GetByNIC), new { nic = newOwner.NIC }, newOwner);
//         }

//         // ✅ Update EV Owner
//         [HttpPut("{nic}")]
//         public async Task<IActionResult> Update(string nic, [FromBody] EvOwner updatedOwner)
//         {
//             var existing = await _evOwnerRepository.GetByNICAsync(nic);
//             if (existing == null)
//                 return NotFound(new { message = "EV Owner not found" });

//             updatedOwner.NIC = nic;
//             if (!string.IsNullOrEmpty(updatedOwner.PasswordHash))
//                 updatedOwner.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updatedOwner.PasswordHash);

//             await _evOwnerRepository.UpdateAsync(nic, updatedOwner);
//             return Ok(new { message = "EV Owner updated successfully" });
//         }

//         // ✅ Deactivate EV Owner
//         [HttpPut("{nic}/deactivate")]
//         public async Task<IActionResult> Deactivate(string nic)
//         {
//             var owner = await _evOwnerRepository.GetByNICAsync(nic);
//             if (owner == null)
//                 return NotFound(new { message = "EV Owner not found" });

//             owner.IsActive = false;
//             await _evOwnerRepository.UpdateAsync(nic, owner);
//             return Ok(new { message = "EV Owner account deactivated" });
//         }

//         // ✅ Delete EV Owner (optional - mostly for testing)
//         [HttpDelete("{nic}")]
//         public async Task<IActionResult> Delete(string nic)
//         {
//             var owner = await _evOwnerRepository.GetByNICAsync(nic);
//             if (owner == null)
//                 return NotFound(new { message = "EV Owner not found" });

//             await _evOwnerRepository.DeleteAsync(nic);
//             return Ok(new { message = "EV Owner deleted successfully" });
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
    public class EvOwnerController : ControllerBase
    {
        private readonly EvOwnerRepository _evOwnerRepository;

        public EvOwnerController(EvOwnerRepository evOwnerRepository)
        {
            _evOwnerRepository = evOwnerRepository;
        }

        // ✅ Get all EV Owners
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var owners = await _evOwnerRepository.GetAllAsync();
            return Ok(owners);
        }

        // ✅ Get EV Owner by NIC
        [HttpGet("{nic}")]
        public async Task<IActionResult> GetByNIC(string nic)
        {
            var owner = await _evOwnerRepository.GetByNICAsync(nic);
            if (owner == null)
                return NotFound(new { message = "EV Owner not found" });
            return Ok(owner);
        }

        // ✅ Create new EV Owner
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] EvOwner newOwner)
        {
            if (string.IsNullOrWhiteSpace(newOwner.NIC) ||
                string.IsNullOrWhiteSpace(newOwner.Email) ||
                string.IsNullOrWhiteSpace(newOwner.PasswordHash))
            {
                return BadRequest(new { message = "NIC, Email, and Password are required" });
            }

            var existing = await _evOwnerRepository.GetByNICAsync(newOwner.NIC);
            if (existing != null)
                return Conflict(new { message = "An EV Owner with this NIC already exists" });

            newOwner.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newOwner.PasswordHash);
            newOwner.IsActive = true;

            await _evOwnerRepository.CreateAsync(newOwner);
            return CreatedAtAction(nameof(GetByNIC), new { nic = newOwner.NIC }, newOwner);
        }

        // ✅ Update EV Owner (only allowed fields)
        [HttpPut("{nic}")]
        public async Task<IActionResult> Update(string nic, [FromBody] EvOwner updated)
        {
            var existing = await _evOwnerRepository.GetByNICAsync(nic);
            if (existing == null)
                return NotFound(new { message = "EV Owner not found" });

            // Merge allowed fields
            existing.Name          = updated.Name          ?? existing.Name;
            existing.Email         = updated.Email         ?? existing.Email;
            existing.Phone         = updated.Phone         ?? existing.Phone;
            existing.VehicleNumber = updated.VehicleNumber ?? existing.VehicleNumber;

            if (!string.IsNullOrWhiteSpace(updated.PasswordHash))
                existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword(updated.PasswordHash);

            await _evOwnerRepository.UpdateAsync(nic, existing);
            return Ok(new { message = "EV Owner updated successfully" });
        }

        // ✅ Deactivate EV Owner
        [HttpPut("{nic}/deactivate")]
        public async Task<IActionResult> Deactivate(string nic)
        {
            var owner = await _evOwnerRepository.GetByNICAsync(nic);
            if (owner == null)
                return NotFound(new { message = "EV Owner not found" });

            owner.IsActive = false;
            await _evOwnerRepository.UpdateAsync(nic, owner);
            return Ok(new { message = "EV Owner account deactivated" });
        }

        // (Optional) Delete EV Owner — mostly for testing
        [HttpDelete("{nic}")]
        public async Task<IActionResult> Delete(string nic)
        {
            var owner = await _evOwnerRepository.GetByNICAsync(nic);
            if (owner == null)
                return NotFound(new { message = "EV Owner not found" });

            await _evOwnerRepository.DeleteAsync(nic);
            return Ok(new { message = "EV Owner deleted successfully" });
        }
    }
}
