// using backend.Models;
// using backend.Repositories;
// using Microsoft.AspNetCore.Mvc;

// namespace backend.Controllers
// {
//     [ApiController]
//     [Route("api/[controller]")]
//     public class StationController : ControllerBase
//     {
//         private readonly StationRepository _stationRepository;

//         public StationController(StationRepository stationRepository)
//         {
//             _stationRepository = stationRepository;
//         }

//         // ✅ Get all stations
//         [HttpGet]
//         public async Task<IActionResult> GetAll()
//         {
//             var stations = await _stationRepository.GetAllAsync();
//             return Ok(stations);
//         }

//         // ✅ Get station by ID
//         [HttpGet("{id}")]
//         public async Task<IActionResult> GetById(string id)
//         {
//             var station = await _stationRepository.GetByIdAsync(id);
//             if (station == null)
//                 return NotFound(new { message = "Charging Station not found" });
//             return Ok(station);
//         }

//         // ✅ Create new station
//         [HttpPost]
//         public async Task<IActionResult> Create([FromBody] ChargingStation newStation)
//         {
//             if (string.IsNullOrWhiteSpace(newStation.Name) ||
//                 string.IsNullOrWhiteSpace(newStation.Location) ||
//                 string.IsNullOrWhiteSpace(newStation.Type))
//             {
//                 return BadRequest(new { message = "Name, Location, and Type are required" });
//             }

//             await _stationRepository.CreateAsync(newStation);
//             return CreatedAtAction(nameof(GetById), new { id = newStation.Id }, newStation);
//         }

//         // ✅ Update station
//         [HttpPut("{id}")]
//         public async Task<IActionResult> Update(string id, [FromBody] ChargingStation updatedStation)
//         {
//             var existing = await _stationRepository.GetByIdAsync(id);
//             if (existing == null)
//                 return NotFound(new { message = "Charging Station not found" });

//             updatedStation.Id = id;
//             await _stationRepository.UpdateAsync(id, updatedStation);
//             return Ok(new { message = "Charging Station updated successfully" });
//         }

//         // ✅ Deactivate station (business rule: cannot deactivate if active bookings exist → will implement later)
//         [HttpPut("{id}/deactivate")]
//         public async Task<IActionResult> Deactivate(string id)
//         {
//             var station = await _stationRepository.GetByIdAsync(id);
//             if (station == null)
//                 return NotFound(new { message = "Charging Station not found" });

//             station.IsActive = false;
//             await _stationRepository.UpdateAsync(id, station);
//             return Ok(new { message = "Station deactivated successfully" });
//         }

//         // ✅ Delete station (mostly for testing)
//         [HttpDelete("{id}")]
//         public async Task<IActionResult> Delete(string id)
//         {
//             var station = await _stationRepository.GetByIdAsync(id);
//             if (station == null)
//                 return NotFound(new { message = "Charging Station not found" });

//             await _stationRepository.DeleteAsync(id);
//             return Ok(new { message = "Station deleted successfully" });
//         }
//     }
// }
using backend.Models;
using backend.Repositories;
using backend.Services; // <-- added
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StationController : ControllerBase
    {
        private readonly StationRepository _stationRepository;
        private readonly StationService _stationService; // <-- added

        public StationController(StationRepository stationRepository, StationService stationService) // <-- added param
        {
            _stationRepository = stationRepository;
            _stationService = stationService; // <-- assign
        }

        // ✅ Get all stations
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var stations = await _stationRepository.GetAllAsync();
            return Ok(stations);
        }

        // ✅ Get station by ID
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var station = await _stationRepository.GetByIdAsync(id);
            if (station == null)
                return NotFound(new { message = "Charging Station not found" });
            return Ok(station);
        }

        // ✅ Create new station (lat/lng optional)
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ChargingStation newStation)
        {
            if (string.IsNullOrWhiteSpace(newStation.Name) ||
                string.IsNullOrWhiteSpace(newStation.Location) ||
                string.IsNullOrWhiteSpace(newStation.Type))
            {
                return BadRequest(new { message = "Name, Location, and Type are required" });
            }

            // Latitude/Longitude are optional (nullable doubles in model)
            await _stationRepository.CreateAsync(newStation);
            return CreatedAtAction(nameof(GetById), new { id = newStation.Id }, newStation);
        }

        // ✅ Update station (partial merge; doesn't wipe missing fields)
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] ChargingStation patch)
        {
            var existing = await _stationRepository.GetByIdAsync(id);
            if (existing == null)
                return NotFound(new { message = "Charging Station not found" });

            // Merge fields if provided
            existing.Name           = patch.Name           ?? existing.Name;
            existing.Location       = patch.Location       ?? existing.Location;
            existing.Type           = patch.Type           ?? existing.Type;
            existing.AvailableSlots = patch.AvailableSlots != 0 ? patch.AvailableSlots : existing.AvailableSlots;
            existing.IsActive       = patch.IsActive;

            // coordinates (nullable)
            if (patch.Latitude.HasValue)  existing.Latitude  = patch.Latitude;
            if (patch.Longitude.HasValue) existing.Longitude = patch.Longitude;

            // schedule (replace only if provided)
            if (patch.Schedule != null && patch.Schedule.Count > 0)
                existing.Schedule = patch.Schedule;

            await _stationRepository.UpdateAsync(id, existing);
            return Ok(new { message = "Charging Station updated successfully" });
        }

        // ✅ Deactivate station (guard: block if future active bookings exist)
        [HttpPut("{id}/deactivate")]
        public async Task<IActionResult> Deactivate(string id)
        {
            try
            {
                var ok = await _stationService.DeactivateAsync(id);
                if (!ok)
                    return BadRequest(new { message = "Cannot deactivate: active future bookings exist." });

                return Ok(new { message = "Station deactivated successfully" });
            }
            catch (KeyNotFoundException)
            {
                return NotFound(new { message = "Charging Station not found" });
            }
        }

        // ✅ Delete station (mostly for testing)
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var station = await _stationRepository.GetByIdAsync(id);
            if (station == null)
                return NotFound(new { message = "Charging Station not found" });

            await _stationRepository.DeleteAsync(id);
            return Ok(new { message = "Station deleted successfully" });
        }
    }
}
