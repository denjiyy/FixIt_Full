using MongoDB.Driver;
using FixIt.Data.Configuration.Contracts;
using FixIt.Models.Locations;

namespace FixIt.Data.Configuration;

public class CityConfiguration : ICollectionConfigurator
{
    public async Task ConfigureAsync(IMongoDatabase db)
    {
        var cities = db.GetCollection<City>("cities");

        // Create indexes first
        await CreateIndexesAsync(cities);

        // Check if cities already exist
        var existingCount = await cities.CountDocumentsAsync(FilterDefinition<City>.Empty);
        
        // Only seed if collection is empty
        if (existingCount > 0)
        {
            Console.WriteLine($"[CityConfiguration] Collection already has {existingCount} documents. Skipping seed.");
            return;
        }

        Console.WriteLine("[CityConfiguration] Starting to seed Bulgarian cities...");

        // Seed all Bulgarian municipal centers
        var seedCities = new[]
        {
            // Sofia City Province (Metropolitan area)
            new City
            {
                Name = "Sofia",
                Country = "Bulgaria",
                Description = "Bulgaria's capital and largest city, known for its historic landmarks and vibrant culture.",
                PhotoUrl = "https://images.unsplash.com/photo-1489749798305-4fea3ba63d60?w=600&h=400&fit=crop"
            },

            // Blagoevgrad Province
            new City
            {
                Name = "Blagoevgrad",
                Country = "Bulgaria",
                Description = "A city in southwestern Bulgaria, known as a cultural center with significant youth population.",
                PhotoUrl = "https://images.unsplash.com/photo-1486306926a5-20a48f31f04b?w=600&h=400&fit=crop"
            },

            // Burgas Province
            new City
            {
                Name = "Burgas",
                Country = "Bulgaria",
                Description = "A major Black Sea port city and beach destination in southeastern Bulgaria.",
                PhotoUrl = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=600&h=400&fit=crop"
            },

            // Varna Province
            new City
            {
                Name = "Varna",
                Country = "Bulgaria",
                Description = "Bulgaria's premier seaside resort city located on the Black Sea coast.",
                PhotoUrl = "https://images.unsplash.com/photo-1476514525535-07fb3b4ae5f1?w=600&h=400&fit=crop"
            },

            // Dobrich Province
            new City
            {
                Name = "Dobrich",
                Country = "Bulgaria",
                Description = "A city in northeastern Bulgaria known for its wheat production and agricultural heritage.",
                PhotoUrl = "https://images.unsplash.com/photo-1495854035989-cebdc8ec6b94?w=600&h=400&fit=crop"
            },

            // Gabrovo Province
            new City
            {
                Name = "Gabrovo",
                Country = "Bulgaria",
                Description = "Located in central Bulgaria, famous for its textile industry and 'Joke Festival'.",
                PhotoUrl = "https://images.unsplash.com/photo-1469854523086-cc02fe5d8800?w=600&h=400&fit=crop"
            },

            // Haskovo Province
            new City
            {
                Name = "Haskovo",
                Country = "Bulgaria",
                Description = "A city in southern Bulgaria situated on the Maritsa River.",
                PhotoUrl = "https://images.unsplash.com/photo-1506905925346-21bda4d32df4?w=600&h=400&fit=crop"
            },

            // Kardjali Province
            new City
            {
                Name = "Kardzhali",
                Country = "Bulgaria",
                Description = "A city in southern Bulgaria known for its Thracian heritage and diamond reserves.",
                PhotoUrl = "https://images.unsplash.com/photo-1489749798305-4fea3ba63d60?w=600&h=400&fit=crop"
            },

            // Kyustendil Province
            new City
            {
                Name = "Kyustendil",
                Country = "Bulgaria",
                Description = "A city in southwestern Bulgaria known for its natural hot springs and wine production.",
                PhotoUrl = "https://images.unsplash.com/photo-1506905925346-21bda4d32df4?w=600&h=400&fit=crop"
            },

            // Lovech Province
            new City
            {
                Name = "Lovech",
                Country = "Bulgaria",
                Description = "A historic city in north-central Bulgaria known for its old town and medieval bridge.",
                PhotoUrl = "https://images.unsplash.com/photo-1485447066519-b21cdc798468?w=600&h=400&fit=crop"
            },

            // Montana Province
            new City
            {
                Name = "Montana",
                Country = "Bulgaria",
                Description = "A city in northwestern Bulgaria with rich Roman and medieval heritage.",
                PhotoUrl = "https://images.unsplash.com/photo-1495854035989-cebdc8ec6b94?w=600&h=400&fit=crop"
            },

            // Pazardzhik Province
            new City
            {
                Name = "Pazardzhik",
                Country = "Bulgaria",
                Description = "Located in south-central Bulgaria, known for rose production and textile industry.",
                PhotoUrl = "https://images.unsplash.com/photo-1469854523086-cc02fe5d8800?w=600&h=400&fit=crop"
            },

            // Pernik Province
            new City
            {
                Name = "Pernik",
                Country = "Bulgaria",
                Description = "An industrial city near Sofia, known for coal mining and metalworking.",
                PhotoUrl = "https://images.unsplash.com/photo-1486306926a5-20a48f31f04b?w=600&h=400&fit=crop"
            },

            // Pleven Province
            new City
            {
                Name = "Pleven",
                Country = "Bulgaria",
                Description = "A major city in northern Bulgaria, historically significant for the Russian-Turkish War.",
                PhotoUrl = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=600&h=400&fit=crop"
            },

            // Plovdiv Province
            new City
            {
                Name = "Plovdiv",
                Country = "Bulgaria",
                Description = "Bulgaria's second-largest city with ancient Thracian and Roman heritage.",
                PhotoUrl = "https://images.unsplash.com/photo-1488646953014-85cb44e25828?w=600&h=400&fit=crop"
            },

            // Razgrad Province
            new City
            {
                Name = "Razgrad",
                Country = "Bulgaria",
                Description = "A city in northeastern Bulgaria known for its beer production and sports tradition.",
                PhotoUrl = "https://images.unsplash.com/photo-1469854523086-cc02fe5d8800?w=600&h=400&fit=crop"
            },

            // Ruse Province
            new City
            {
                Name = "Ruse",
                Country = "Bulgaria",
                Description = "A port city on the Danube River in northern Bulgaria with Austro-Hungarian architecture.",
                PhotoUrl = "https://images.unsplash.com/photo-1476514525535-07fb3b4ae5f1?w=600&h=400&fit=crop"
            },

            // Shumen Province
            new City
            {
                Name = "Shumen",
                Country = "Bulgaria",
                Description = "A historic city in northeastern Bulgaria known for its fortress and cultural heritage.",
                PhotoUrl = "https://images.unsplash.com/photo-1495854035989-cebdc8ec6b94?w=600&h=400&fit=crop"
            },

            // Silistra Province
            new City
            {
                Name = "Silistra",
                Country = "Bulgaria",
                Description = "A Danube port city in northeastern Bulgaria with Roman and medieval monuments.",
                PhotoUrl = "https://images.unsplash.com/photo-1506905925346-21bda4d32df4?w=600&h=400&fit=crop"
            },

            // Sliven Province
            new City
            {
                Name = "Sliven",
                Country = "Bulgaria",
                Description = "A city in eastern Bulgaria known for textile production and the 'Town of Hundred Voivodes'.",
                PhotoUrl = "https://images.unsplash.com/photo-1489749798305-4fea3ba63d60?w=600&h=400&fit=crop"
            },

            // Smolyan Province
            new City
            {
                Name = "Smolyan",
                Country = "Bulgaria",
                Description = "A mountain city in southern Bulgaria known for cool climate and skiing.",
                PhotoUrl = "https://images.unsplash.com/photo-1486306926a5-20a48f31f04b?w=600&h=400&fit=crop"
            },

            // Sofia Province (excluding Sofia City itself)
            new City
            {
                Name = "Svoge",
                Country = "Bulgaria",
                Description = "A town in Sofia Province known for its mineral water.",
                PhotoUrl = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=600&h=400&fit=crop"
            },

            // Stara Zagora Province
            new City
            {
                Name = "Stara Zagora",
                Country = "Bulgaria",
                Description = "A city in south-central Bulgaria with significant sports infrastructure.",
                PhotoUrl = "https://images.unsplash.com/photo-1488646953014-85cb44e25828?w=600&h=400&fit=crop"
            },

            // Targovishte Province
            new City
            {
                Name = "Targovishte",
                Country = "Bulgaria",
                Description = "A city in northern Bulgaria with historical and cultural significance.",
                PhotoUrl = "https://images.unsplash.com/photo-1469854523086-cc02fe5d8800?w=600&h=400&fit=crop"
            },

            // Vidin Province
            new City
            {
                Name = "Vidin",
                Country = "Bulgaria",
                Description = "A Danube port city in northwestern Bulgaria with well-preserved fortifications.",
                PhotoUrl = "https://images.unsplash.com/photo-1476514525535-07fb3b4ae5f1?w=600&h=400&fit=crop"
            },

            // Yambol Province
            new City
            {
                Name = "Yambol",
                Country = "Bulgaria",
                Description = "A city in southeastern Bulgaria with ancient Thracian ruins nearby.",
                PhotoUrl = "https://images.unsplash.com/photo-1495854035989-cebdc8ec6b94?w=600&h=400&fit=crop"
            }
        };

        // Insert all cities at once
        await cities.InsertManyAsync(seedCities);
        Console.WriteLine($"[CityConfiguration] Successfully seeded {seedCities.Length} Bulgarian cities.");
    }

    private async Task CreateIndexesAsync(IMongoCollection<City> cities)
    {
        try
        {
            // Index for city lookup
            var nameIndex = new CreateIndexModel<City>(
                Builders<City>.IndexKeys.Ascending(c => c.Name),
                new CreateIndexOptions { Name = "ix_cities_name" }
            );
            await cities.Indexes.CreateOneAsync(nameIndex);

            // Index for country lookups to support filtering by country
            var countryIndex = new CreateIndexModel<City>(
                Builders<City>.IndexKeys.Ascending(c => c.Country),
                new CreateIndexOptions { Name = "ix_cities_country" }
            );
            await cities.Indexes.CreateOneAsync(countryIndex);

            // Compound index for country + name queries
            var countryNameIndex = new CreateIndexModel<City>(
                Builders<City>.IndexKeys
                    .Ascending(c => c.Country)
                    .Ascending(c => c.Name),
                new CreateIndexOptions { Name = "ix_cities_country_name" }
            );
            await cities.Indexes.CreateOneAsync(countryNameIndex);

            Console.WriteLine("[CityConfiguration] Indexes created successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CityConfiguration] Error creating indexes: {ex.Message}");
        }
    }
}
