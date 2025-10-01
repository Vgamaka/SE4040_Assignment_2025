using backend.Models;
using backend.Repositories;
using backend.Services;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson; // for ObjectId.TryParse & GenerateNewId

namespace backend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SessionsController : ControllerBase
    {
        private readonly BookingRepository _bookingRepository;
        private readonly SessionRepository _sessionRepository;
        private readonly JwtTokenService _jwt;

        public SessionsController(
            BookingRepository bookingRepository,
            SessionRepository sessionRepository,
            JwtTokenService jwt)
        {
            _bookingRepository = bookingRepository;
            _sessionRepository = sessionRepository;
            _jwt = jwt;
        }

        /// <summary>
        /// Finalize a booking after scanning a QR token.
        /// Token format: "booking:&lt;id&gt;;ts:&lt;ticks&gt;|&lt;hmac&gt;"
        /// </summary>
        [HttpPost("finalize")]
        public async Task<IActionResult> Finalize([FromBody] QrFinalizeRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Token))
                return BadRequest(new { message = "Token is required" });

            // Split token parts: left=raw, right=hmac
            var parts = req.Token.Split('|');
            if (parts.Length != 2)
                return BadRequest(new { message = "Invalid token format" });

            var raw = parts[0];
            var hmac = parts[1];

            // Verify checksum (HMAC over the raw part)
            if (_jwt.HmacBase64Url(raw) != hmac)
                return BadRequest(new { message = "Invalid token checksum" });

            // Extract booking id from raw
            // raw example: "booking:<id>;ts:<ticks>"
            var kv = raw.Split(';'); // ["booking:<id>", "ts:<ticks>"]
            var bid = kv.FirstOrDefault(x => x.StartsWith("booking:"))?.Split(':').Last();

            if (string.IsNullOrWhiteSpace(bid))
                return BadRequest(new { message = "Invalid booking id" });

            // Ensure the id is a valid Mongo ObjectId to avoid FormatException down the stack
            if (!ObjectId.TryParse(bid, out _))
                return BadRequest(new { message = "Invalid booking id format" });

            // Fetch booking
            var booking = await _bookingRepository.GetByIdAsync(bid);
            if (booking == null)
                return NotFound(new { message = "Booking not found" });

            // Mark booking as completed
            booking.Status = "Completed";
            await _bookingRepository.UpdateAsync(booking);

            // Create new session with a proper Mongo ObjectId as string
            var session = new Session
            {
                Id = ObjectId.GenerateNewId().ToString(),
                BookingId = booking.Id,
                StationId = booking.StationId,
                OperatorUserId = User?.Identity?.Name, // optional operator identity if JWT carries it
                StartedAtUtc = DateTime.UtcNow,
                EndedAtUtc = DateTime.UtcNow,
                Status = "Completed"
            };

            await _sessionRepository.InsertAsync(session);

            return Ok(new { bookingId = booking.Id, status = booking.Status });
        }
    }
}
