using backend.Models;
using MongoDB.Driver;

namespace backend.Repositories
{
    public class StationRepository
    {
        private readonly IMongoCollection<ChargingStation> _stations;

        public StationRepository(IMongoDatabase database)
        {
            _stations = database.GetCollection<ChargingStation>("stations");
        }

        public async Task<List<ChargingStation>> GetAllAsync()
        {
            return await _stations.Find(_ => true).ToListAsync();
        }

        public async Task<ChargingStation?> GetByIdAsync(string id)
        {
            return await _stations.Find(s => s.Id == id).FirstOrDefaultAsync();
        }

        public async Task CreateAsync(ChargingStation station)
        {
            await _stations.InsertOneAsync(station);
        }

        public async Task UpdateAsync(string id, ChargingStation updatedStation)
        {
            await _stations.ReplaceOneAsync(s => s.Id == id, updatedStation);
        }

        public async Task DeleteAsync(string id)
        {
            await _stations.DeleteOneAsync(s => s.Id == id);
        }
    }
}
