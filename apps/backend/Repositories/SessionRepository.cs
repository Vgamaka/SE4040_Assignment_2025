using backend.Models;
using MongoDB.Driver;

namespace backend.Repositories
{
    /// <summary>
    /// Repository for managing charging sessions.
    /// </summary>
    public class SessionRepository
    {
        private readonly IMongoCollection<Session> _sessions;

        public SessionRepository(IMongoDatabase database)
        {
            _sessions = database.GetCollection<Session>("sessions");
        }

        // === Basic CRUD ===
        public async Task<Session> InsertAsync(Session session)
        {
            await _sessions.InsertOneAsync(session);
            return session;
        }

        public async Task<Session?> GetByIdAsync(string id) =>
            await _sessions.Find(s => s.Id == id).FirstOrDefaultAsync();

        public async Task<List<Session>> GetAllAsync() =>
            await _sessions.Find(_ => true).ToListAsync();

        // === Extra helpers ===

        /// <summary>
        /// Returns all sessions linked to a specific booking.
        /// </summary>
        public async Task<List<Session>> ForBookingAsync(string bookingId) =>
            await _sessions.Find(s => s.BookingId == bookingId).ToListAsync();

        /// <summary>
        /// Returns all sessions linked to a specific station.
        /// </summary>
        public async Task<List<Session>> ForStationAsync(string stationId) =>
            await _sessions.Find(s => s.StationId == stationId).ToListAsync();
    }
}
