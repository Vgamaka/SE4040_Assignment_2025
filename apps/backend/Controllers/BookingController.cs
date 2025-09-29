using backend.Models;
using backend.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BookingController : ControllerBase
    {
        private readonly BookingRepository _bookingRepository;
        private readonly StationRepository _stationRepository;
        private readonly EvOwnerRepository _evOwnerRepository;

        public BookingController(
            BookingRepository bookingRepository,
            StationRepository stationRepository,
            EvOwnerRepository evOwnerRepository)
        {
            _bookingRepository = bookingRepository;
            _stationRepository = stationRepository;
            _evOwnerRepository = evOwnerRepository;
        }

        // ✅ Get all bookings
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var bookings = await _bookingRepository.GetAllAsync();
            return Ok(bookings);
        }

        // ✅ Get booking by ID
        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(string id)
        {
            var booking = await _bookingRepository.GetByIdAsync(id);
            if (booking == null)
                return NotFound(new { message = "Booking not found" });
            return Ok(booking);
        }

        // ✅ Get bookings by Owner NIC
        [HttpGet("owner/{nic}")]
        public async Task<IActionResult> GetByOwner(string nic)
        {
            var bookings = await _bookingRepository.GetByOwnerAsync(nic);
            return Ok(bookings);
        }

        // ✅ Create a new booking with business rules
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] Booking newBooking)
        {
            if (string.IsNullOrWhiteSpace(newBooking.OwnerNIC) || string.IsNullOrWhiteSpace(newBooking.StationId))
                return BadRequest(new { message = "OwnerNIC and StationId are required" });

            // Rule 1: Reservation must be within 7 days
            var maxReservationDate = DateTime.UtcNow.AddDays(7);
            if (newBooking.ReservationDateTime > maxReservationDate)
                return BadRequest(new { message = "Reservation must be within 7 days from today" });

            // Rule 2: Owner must exist
            var owner = await _evOwnerRepository.GetByNICAsync(newBooking.OwnerNIC);
            if (owner == null)
                return BadRequest(new { message = "EV Owner not found" });

            // Rule 3: Station must exist and be active
            var station = await _stationRepository.GetByIdAsync(newBooking.StationId);
            if (station == null || !station.IsActive)
                return BadRequest(new { message = "Charging station is invalid or inactive" });

            // Save booking
            newBooking.Status = "Pending";
            newBooking.CreatedAt = DateTime.UtcNow;
            await _bookingRepository.CreateAsync(newBooking);

            return CreatedAtAction(nameof(GetById), new { id = newBooking.Id }, newBooking);
        }

        // ✅ Update booking (must be 12+ hours before reservation)
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(string id, [FromBody] Booking updatedBooking)
        {
            var existing = await _bookingRepository.GetByIdAsync(id);
            if (existing == null)
                return NotFound(new { message = "Booking not found" });

            var timeDiff = existing.ReservationDateTime - DateTime.UtcNow;
            if (timeDiff.TotalHours < 12)
                return BadRequest(new { message = "Cannot update booking within 12 hours of reservation time" });

            updatedBooking.Id = id;
            updatedBooking.Status = existing.Status; // preserve status
            await _bookingRepository.UpdateAsync(id, updatedBooking);

            return Ok(new { message = "Booking updated successfully" });
        }

        // ✅ Cancel booking (must be 12+ hours before reservation)
        [HttpPut("{id}/cancel")]
        public async Task<IActionResult> Cancel(string id)
        {
            var booking = await _bookingRepository.GetByIdAsync(id);
            if (booking == null)
                return NotFound(new { message = "Booking not found" });

            var timeDiff = booking.ReservationDateTime - DateTime.UtcNow;
            if (timeDiff.TotalHours < 12)
                return BadRequest(new { message = "Cannot cancel booking within 12 hours of reservation time" });

            booking.Status = "Cancelled";
            await _bookingRepository.UpdateAsync(id, booking);

            return Ok(new { message = "Booking cancelled successfully" });
        }

        // ✅ Approve booking + generate QR code (dummy)
        [HttpPut("{id}/approve")]
        public async Task<IActionResult> Approve(string id)
        {
            var booking = await _bookingRepository.GetByIdAsync(id);
            if (booking == null)
                return NotFound(new { message = "Booking not found" });

            // Generate a simple QR code string (in real app you'd generate an image)
            booking.Status = "Approved";
            booking.QrCode = $"QR-{Guid.NewGuid()}";
            await _bookingRepository.UpdateAsync(id, booking);

            return Ok(new { message = "Booking approved", qrCode = booking.QrCode });
        }

        // ✅ Delete booking (testing only)
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(string id)
        {
            var booking = await _bookingRepository.GetByIdAsync(id);
            if (booking == null)
                return NotFound(new { message = "Booking not found" });

            await _bookingRepository.DeleteAsync(id);
            return Ok(new { message = "Booking deleted successfully" });
        }
    }
}
