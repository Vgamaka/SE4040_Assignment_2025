using backend.Models;
using backend.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StationController : ControllerBase
    {
        private readonly StationRepository _stationRepository;

        public StationController(StationRepository stationRepository)
        {
            _stationRepository = stationRepository;
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

        // ✅ Create new station
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] ChargingStation newStation)
        {
            if (string.IsNullOrWhiteSpace(newStation.Name) ||
                string.IsNullOrWhiteSpace(newStation.Location) ||
                string.IsNullOrWhiteSpace(newStation.Type))
            {
                return BadRequest(new { message = "Name, Location, and Type are required" });
            }

            await _stationRepository.CreateAsync(newStation);
            return CreatedAtAction(nameof(GetById), new { id = newStation.Id }, newStation);
        }

        // ✅ Update station
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] ChargingStation updatedStation)
        {
            var existing = await _stationRepository.GetByIdAsync(id);
            if (existing == null)
                return NotFound(new { message = "Charging Station not found" });

            updatedStation.Id = id;
            await _stationRepository.UpdateAsync(id, updatedStation);
            return Ok(new { message = "Charging Station updated successfully" });
        }

        // ✅ Deactivate station (business rule: cannot deactivate if active bookings exist → will implement later)
        [HttpPut("{id}/deactivate")]
        public async Task<IActionResult> Deactivate(string id)
        {
            var station = await _stationRepository.GetByIdAsync(id);
            if (station == null)
                return NotFound(new { message = "Charging Station not found" });

            station.IsActive = false;
            await _stationRepository.UpdateAsync(id, station);
            return Ok(new { message = "Station deactivated successfully" });
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
