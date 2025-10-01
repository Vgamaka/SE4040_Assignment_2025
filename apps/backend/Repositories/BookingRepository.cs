// using backend.Models;
// using MongoDB.Driver;

// namespace backend.Repositories
// {
//     public class BookingRepository
//     {
//         private readonly IMongoCollection<Booking> _bookings;

//         public BookingRepository(IMongoDatabase database)
//         {
//             _bookings = database.GetCollection<Booking>("bookings");
//         }

//         public async Task<List<Booking>> GetAllAsync()
//         {
//             return await _bookings.Find(_ => true).ToListAsync();
//         }

//         public async Task<Booking?> GetByIdAsync(string id)
//         {
//             return await _bookings.Find(b => b.Id == id).FirstOrDefaultAsync();
//         }

//         public async Task<List<Booking>> GetByOwnerAsync(string ownerNIC)
//         {
//             return await _bookings.Find(b => b.OwnerNIC == ownerNIC).ToListAsync();
//         }

//         public async Task CreateAsync(Booking booking)
//         {
//             await _bookings.InsertOneAsync(booking);
//         }

//         public async Task UpdateAsync(string id, Booking updatedBooking)
//         {
//             await _bookings.ReplaceOneAsync(b => b.Id == id, updatedBooking);
//         }

//         public async Task DeleteAsync(string id)
//         {
//             await _bookings.DeleteOneAsync(b => b.Id == id);
//         }
//     }
// }
using backend.Models;
using MongoDB.Bson;
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

        // === Basic CRUD ===
        public async Task<List<Booking>> GetAllAsync() =>
            await _bookings.Find(_ => true).ToListAsync();

        public async Task<Booking?> GetByIdAsync(string id)
        {
            // Avoid FormatException when id is not a valid ObjectId
            if (!ObjectId.TryParse(id, out var oid)) return null;
            var filter = Builders<Booking>.Filter.Eq("_id", oid);
            return await _bookings.Find(filter).FirstOrDefaultAsync();
        }

        public async Task<List<Booking>> GetByOwnerAsync(string ownerNIC) =>
            await _bookings.Find(b => b.OwnerNIC == ownerNIC).ToListAsync();

        public async Task<Booking> InsertAsync(Booking booking)
        {
            await _bookings.InsertOneAsync(booking);
            return booking;
        }

        public async Task UpdateAsync(Booking updatedBooking)
        {
            if (string.IsNullOrWhiteSpace(updatedBooking.Id) || !ObjectId.TryParse(updatedBooking.Id, out var oid))
                throw new ArgumentException("Booking Id is invalid for update");

            var filter = Builders<Booking>.Filter.Eq("_id", oid);
            await _bookings.ReplaceOneAsync(filter, updatedBooking);
        }

        public async Task DeleteAsync(string id)
        {
            if (!ObjectId.TryParse(id, out var oid)) return; // no-op if invalid
            var filter = Builders<Booking>.Filter.Eq("_id", oid);
            await _bookings.DeleteOneAsync(filter);
        }

        // === Extra helpers for rules & dashboards ===

        /// <summary>
        /// Count future bookings for a given owner by status.
        /// </summary>
        public async Task<long> CountFutureByStatusAsync(string ownerNic, string status) =>
            await _bookings.CountDocumentsAsync(b =>
                b.OwnerNIC == ownerNic &&
                b.Status == status &&
                b.StartUtc > DateTime.UtcNow);

        /// <summary>
        /// Get bookings that ended before now (past history).
        /// </summary>
        public async Task<List<Booking>> GetPastBookingsAsync(string ownerNic) =>
            await _bookings.Find(b =>
                b.OwnerNIC == ownerNic &&
                b.EndUtc < DateTime.UtcNow)
                .ToListAsync();
                /// <summary>
        /// Count bookings for an owner by exact status (no time filter).
        /// </summary>
        public async Task<long> CountByStatusAsync(string ownerNic, string status) =>
            await _bookings.CountDocumentsAsync(b =>
                b.OwnerNIC == ownerNic &&
                b.Status == status);

    }
    
}
