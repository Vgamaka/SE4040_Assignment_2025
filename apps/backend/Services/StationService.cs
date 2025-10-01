using backend.Models;
using backend.Repositories;

namespace backend.Services
{
    /// <summary>
    /// Business logic for stations (create, partial update, safe deactivate).
    /// Controller can call into here to keep actions thin and centralize rules.
    /// </summary>
    public class StationService
    {
        private readonly StationRepository _stationRepo;
        private readonly BookingRepository _bookingRepo;

        public StationService(StationRepository stationRepo, BookingRepository bookingRepo)
        {
            _stationRepo = stationRepo;
            _bookingRepo = bookingRepo;
        }

        public async Task<List<ChargingStation>> GetAllAsync() =>
            await _stationRepo.GetAllAsync();

        public async Task<ChargingStation?> GetByIdAsync(string id) =>
            await _stationRepo.GetByIdAsync(id);

        public async Task<ChargingStation> CreateAsync(ChargingStation station)
        {
            if (string.IsNullOrWhiteSpace(station.Name) ||
                string.IsNullOrWhiteSpace(station.Location) ||
                string.IsNullOrWhiteSpace(station.Type))
            {
                throw new ArgumentException("Name, Location, and Type are required");
            }

            await _stationRepo.CreateAsync(station);
            return station;
        }

        /// <summary>
        /// Applies a partial update (merge) to a station.
        /// </summary>
        public async Task<ChargingStation> UpdatePartialAsync(string id, ChargingStation patch)
        {
            var existing = await _stationRepo.GetByIdAsync(id)
                           ?? throw new KeyNotFoundException("Charging Station not found");

            existing.Name           = patch.Name           ?? existing.Name;
            existing.Location       = patch.Location       ?? existing.Location;
            existing.Type           = patch.Type           ?? existing.Type;
            if (patch.AvailableSlots != 0)
                existing.AvailableSlots = patch.AvailableSlots;

            // coordinates (nullable)
            if (patch.Latitude.HasValue)  existing.Latitude  = patch.Latitude;
            if (patch.Longitude.HasValue) existing.Longitude = patch.Longitude;

            // schedule (replace only if provided)
            if (patch.Schedule != null && patch.Schedule.Count > 0)
                existing.Schedule = patch.Schedule;

            // IsActive is not toggled here; use DeactivateAsync to apply guard rules
            await _stationRepo.UpdateAsync(id, existing);
            return existing;
        }

        /// <summary>
        /// Attempts to deactivate a station. Returns false if blocked by active bookings.
        /// Active means Approved or Pending with StartUtc in the future.
        /// </summary>
        public async Task<bool> DeactivateAsync(string id)
        {
            var station = await _stationRepo.GetByIdAsync(id)
                          ?? throw new KeyNotFoundException("Charging Station not found");

            // NOTE: BookingRepository doesn't have a query by station;
            // we fetch all and filter in-memory to avoid changing repository contracts.
            var allBookings = await _bookingRepo.GetAllAsync();

            var hasActiveFutureBookings = allBookings.Any(b =>
                b.StationId == id &&
                (b.Status == "Approved" || b.Status == "Pending") &&
                b.StartUtc > DateTime.UtcNow
            );

            if (hasActiveFutureBookings)
            {
                // Guard rule: do not deactivate; caller can show a friendly error.
                return false;
            }

            station.IsActive = false;
            await _stationRepo.UpdateAsync(id, station);
            return true;
        }
    }
}
