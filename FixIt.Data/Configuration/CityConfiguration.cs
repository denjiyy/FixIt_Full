using MongoDB.Driver;
using FixIt.Data.Configuration.Contracts;
using FixIt.Models.Locations;

namespace FixIt.Data.Configuration;

public class CityConfiguration : ICollectionConfigurator
{
    public async Task ConfigureAsync(IMongoDatabase db)
    {
        var cities = db.GetCollection<City>("cities");

        // Index for city lookup
        var nameIndex = new CreateIndexModel<City>(
            Builders<City>.IndexKeys.Ascending(c => c.Name),
            new CreateIndexOptions { Name = "ix_cities_name" }
        );
        await cities.Indexes.CreateOneAsync(nameIndex);

        // Seed initial cities
        var seedCities = new[]
        {
            new City { Name = "Sofia", Country = "Bulgaria" },
            new City { Name = "Plovdiv", Country = "Bulgaria" },
            new City { Name = "Varna", Country = "Bulgaria" }
        };

        foreach (var city in seedCities)
        {
            var filter = Builders<City>.Filter.Eq(c => c.Name, city.Name);
            var options = new ReplaceOptions { IsUpsert = true };
            await cities.ReplaceOneAsync(filter, city, options);
        }
    }
}