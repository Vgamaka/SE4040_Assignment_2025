using backend.Models;
using backend.Repositories;

namespace backend.Services
{
    /// <summary>
    /// Handles business logic for bookings: rule checks and QR token generation.
    /// </summary>
    public class BookingService
    {
        private readonly BookingRepository _repo;
        private readonly JwtTokenService _jwt;

        public BookingService(BookingRepository repo, JwtTokenService jwt)
        {
            _repo = repo;
            _jwt = jwt;
        }

        /// <summary>
        /// Creates a booking with rule enforcement and QR generation.
        /// </summary>
        public async Task<Booking> CreateAsync(Booking b)
        {
            // ---- Normalize legacy field if provided ----
            // If a client still sends ReservationDateTime and StartUtc is default, map it.
            if (b.StartUtc == default && b.ReservationDateTime != default)
            {
                b.StartUtc = b.ReservationDateTime;
                if (b.EndUtc == default)
                {
                    // Sensible default: 1-hour slot if end not provided
                    b.EndUtc = b.StartUtc.AddHours(1);
                }
            }

            // ---- Basic validations ----
            if (b.StartUtc == default)
                throw new InvalidOperationException("StartUtc is required.");
            if (b.EndUtc == default)
                throw new InvalidOperationException("EndUtc is required.");
            if (b.EndUtc <= b.StartUtc)
                throw new InvalidOperationException("EndUtc must be after StartUtc.");

            // Must be in the future
            if (b.StartUtc <= DateTime.UtcNow)
                throw new InvalidOperationException("Booking must start in the future.");

            // Enforce ≤ 7-day window rule
            var delta = (b.StartUtc - DateTime.UtcNow);
            if (delta > TimeSpan.FromDays(7))
                throw new InvalidOperationException("Booking must be within the next 7 days.");

            // ---- Status & timestamps ----
            b.Status = "Approved";               // auto-approve for now (adjust if manual later)
            b.ApprovedAtUtc = DateTime.UtcNow;

            // 1) Insert first so Mongo assigns the real _id
            await _repo.InsertAsync(b);          // after this, b.Id is the ObjectId string

            // 2) Generate QR token & checksum using the REAL id
            var raw = $"booking:{b.Id};ts:{DateTime.UtcNow.Ticks}";
            b.QrToken = raw;
            b.QrChecksum = _jwt.HmacBase64Url(raw);

            // 3) Persist QR fields
            await _repo.UpdateAsync(b);

            return b;
        }

        /// <summary>
        /// Ensures a booking can still be modified or cancelled (>= 12 hours before start).
        /// </summary>
        public void EnsureModifiable(Booking b)
        {
            var hours = (b.StartUtc - DateTime.UtcNow).TotalHours;
            if (hours < 12)
                throw new InvalidOperationException("Cannot modify/cancel within 12 hours of start.");
        }

        /// <summary>
        /// Builds the QR image URL for a booking.
        /// </summary>
        public string BuildQrUrl(string bookingId) => $"/api/Booking/{bookingId}/qr.png";
    }
}
