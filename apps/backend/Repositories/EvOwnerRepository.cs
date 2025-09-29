using backend.Models;
using MongoDB.Driver;

namespace backend.Repositories
{
    public class EvOwnerRepository
    {
        private readonly IMongoCollection<EvOwner> _owners;

        public EvOwnerRepository(IMongoDatabase database)
        {
            _owners = database.GetCollection<EvOwner>("evowners");
        }

        public async Task<List<EvOwner>> GetAllAsync()
        {
            return await _owners.Find(_ => true).ToListAsync();
        }

        public async Task<EvOwner?> GetByNICAsync(string nic)
        {
            return await _owners.Find(o => o.NIC == nic).FirstOrDefaultAsync();
        }

        public async Task CreateAsync(EvOwner owner)
        {
            await _owners.InsertOneAsync(owner);
        }

        public async Task UpdateAsync(string nic, EvOwner updatedOwner)
        {
            await _owners.ReplaceOneAsync(o => o.NIC == nic, updatedOwner);
        }

        public async Task DeleteAsync(string nic)
        {
            await _owners.DeleteOneAsync(o => o.NIC == nic);
        }
    }
}
