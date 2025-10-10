using Microsoft.Extensions.Options;
using MongoDB.Driver;
using EvCharge.Api.Options;

namespace EvCharge.Api.Infrastructure
{
    public static class MongoDb
    {
        public static IServiceCollection AddMongo(this IServiceCollection services)
        {
            services.AddSingleton<IMongoClient>(sp =>
            {
                var mongo = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
                return new MongoClient(mongo.ConnectionString);
            });

            services.AddSingleton(sp =>
            {
                var mongo = sp.GetRequiredService<IOptions<MongoOptions>>().Value;
                var client = sp.GetRequiredService<IMongoClient>();
                return client.GetDatabase(mongo.Database);
            });

            return services;
        }
    }
}
