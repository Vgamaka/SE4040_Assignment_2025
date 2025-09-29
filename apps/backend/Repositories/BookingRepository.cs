using backend.Models;
using MongoDB.Driver;

namespace backend.Repositories
{
    public class BookingRepository
    {
        private readonly IMongoCollection<Booking> _bookings;

        public BookingRepository(IMongoDatabase database)
        {
            _bookings = database.GetCollection<Booking>("bookings");
        }

        public async Task<List<Booking>> GetAllAsync()
        {
            return await _bookings.Find(_ => true).ToListAsync();
        }

        public async Task<Booking?> GetByIdAsync(string id)
        {
            return await _bookings.Find(b => b.Id == id).FirstOrDefaultAsync();
        }

        public async Task<List<Booking>> GetByOwnerAsync(string ownerNIC)
        {
            return await _bookings.Find(b => b.OwnerNIC == ownerNIC).ToListAsync();
        }

        public async Task CreateAsync(Booking booking)
        {
            await _bookings.InsertOneAsync(booking);
        }

        public async Task UpdateAsync(string id, Booking updatedBooking)
        {
            await _bookings.ReplaceOneAsync(b => b.Id == id, updatedBooking);
        }

        public async Task DeleteAsync(string id)
        {
            await _bookings.DeleteOneAsync(b => b.Id == id);
        }
    }
}
