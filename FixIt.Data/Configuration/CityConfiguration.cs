using MongoDB.Driver;
using FixIt.Data.Configuration.Contracts;
using FixIt.Models.Locations;

namespace FixIt.Data.Configuration;

public class CityConfiguration : ICollectionConfigurator
{
    public async Task ConfigureAsync(IMongoDatabase db)
    {
        // Check if cities already exist and have coordinates - if not, drop and reseed
        var existingCount = await db.GetCollection<City>("cities").CountDocumentsAsync(FilterDefinition<City>.Empty);
        
        if (existingCount > 0)
        {
            // Check if existing cities have coordinates
            var firstCity = await db.GetCollection<City>("cities").Find(FilterDefinition<City>.Empty).FirstOrDefaultAsync();
            if (firstCity != null && firstCity.Latitude == 0 && firstCity.Longitude == 0)
            {
                Console.WriteLine($"[CityConfiguration] Existing cities are missing coordinates. Dropping collection to reseed...");
                await db.DropCollectionAsync("cities");
            }
            else if (firstCity != null && firstCity.Latitude != 0 && firstCity.Longitude != 0)
            {
                Console.WriteLine($"[CityConfiguration] Collection already has {existingCount} documents with coordinates. Skipping seed.");
                return;
            }
        }

        var cities = db.GetCollection<City>("cities");

        // Create indexes
        await CreateIndexesAsync(cities);

        Console.WriteLine("[CityConfiguration] Starting to seed Bulgarian cities...");

        // Seed all Bulgarian municipal centers with coordinates
        var seedCities = new[]
        {
            // Sofia City Province (Metropolitan area)
            new City
            {
                Name = "Sofia",
                Country = "Bulgaria",
                Latitude = 42.6977,
                Longitude = 23.3219,
                Description = "Bulgaria's capital and largest city, known for its historic landmarks and vibrant culture.",
                PhotoUrl = "https://images.unsplash.com/photo-1489749798305-4fea3ba63d60?w=600&h=400&fit=crop"
            },

            // Blagoevgrad Province
            new City
            {
                Name = "Blagoevgrad",
                Country = "Bulgaria",
                Latitude = 42.3000,
                Longitude = 23.0980,
                Description = "A city in southwestern Bulgaria, known as a cultural center with significant youth population.",
                PhotoUrl = "https://images.unsplash.com/photo-1486306926a5-20a48f31f04b?w=600&h=400&fit=crop"
            },

            // Burgas Province
            new City
            {
                Name = "Burgas",
                Country = "Bulgaria",
                Latitude = 42.5047,
                Longitude = 27.4711,
                Description = "A major Black Sea port city and beach destination in southeastern Bulgaria.",
                PhotoUrl = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=600&h=400&fit=crop"
            },

            // Varna Province
            new City
            {
                Name = "Varna",
                Country = "Bulgaria",
                Latitude = 43.2141,
                Longitude = 27.9147,
                Description = "Bulgaria's premier seaside resort city located on the Black Sea coast.",
                PhotoUrl = "https://images.unsplash.com/photo-1476514525535-07fb3b4ae5f1?w=600&h=400&fit=crop"
            },

            // Dobrich Province
            new City
            {
                Name = "Dobrich",
                Country = "Bulgaria",
                Latitude = 43.5704,
                Longitude = 28.7626,
                Description = "A city in northeastern Bulgaria known for its wheat production and agricultural heritage.",
                PhotoUrl = "https://images.unsplash.com/photo-1495854035989-cebdc8ec6b94?w=600&h=400&fit=crop"
            },

            // Gabrovo Province
            new City
            {
                Name = "Gabrovo",
                Country = "Bulgaria",
                Latitude = 43.2744,
                Longitude = 24.8639,
                Description = "Located in central Bulgaria, famous for its textile industry and 'Joke Festival'.",
                PhotoUrl = "https://images.unsplash.com/photo-1469854523086-cc02fe5d8800?w=600&h=400&fit=crop"
            },

            // Haskovo Province
            new City
            {
                Name = "Haskovo",
                Country = "Bulgaria",
                Latitude = 41.9294,
                Longitude = 25.5500,
                Description = "A city in southern Bulgaria situated on the Maritsa River.",
                PhotoUrl = "https://images.unsplash.com/photo-1506905925346-21bda4d32df4?w=600&h=400&fit=crop"
            },

            // Kardjali Province
            new City
            {
                Name = "Kardzhali",
                Country = "Bulgaria",
                Latitude = 41.6478,
                Longitude = 25.4185,
                Description = "A city in southern Bulgaria known for its Thracian heritage and diamond reserves.",
                PhotoUrl = "https://images.unsplash.com/photo-1489749798305-4fea3ba63d60?w=600&h=400&fit=crop"
            },

            // Kyustendil Province
            new City
            {
                Name = "Kyustendil",
                Country = "Bulgaria",
                Latitude = 42.2842,
                Longitude = 22.6951,
                Description = "A city in southwestern Bulgaria known for its natural hot springs and wine production.",
                PhotoUrl = "https://images.unsplash.com/photo-1506905925346-21bda4d32df4?w=600&h=400&fit=crop"
            },

            // Lovech Province
            new City
            {
                Name = "Lovech",
                Country = "Bulgaria",
                Latitude = 43.1382,
                Longitude = 24.7202,
                Description = "A historic city in north-central Bulgaria known for its old town and medieval bridge.",
                PhotoUrl = "https://images.unsplash.com/photo-1485447066519-b21cdc798468?w=600&h=400&fit=crop"
            },

            // Montana Province
            new City
            {
                Name = "Montana",
                Country = "Bulgaria",
                Latitude = 43.4097,
                Longitude = 23.2261,
                Description = "A city in northwestern Bulgaria with rich Roman and medieval heritage.",
                PhotoUrl = "https://images.unsplash.com/photo-1495854035989-cebdc8ec6b94?w=600&h=400&fit=crop"
            },

            // Pazardzhik Province
            new City
            {
                Name = "Pazardzhik",
                Country = "Bulgaria",
                Latitude = 42.1934,
                Longitude = 24.3281,
                Description = "Located in south-central Bulgaria, known for rose production and textile industry.",
                PhotoUrl = "https://images.unsplash.com/photo-1469854523086-cc02fe5d8800?w=600&h=400&fit=crop"
            },

            // Pernik Province
            new City
            {
                Name = "Pernik",
                Country = "Bulgaria",
                Latitude = 42.6042,
                Longitude = 22.9619,
                Description = "An industrial city near Sofia, known for coal mining and metalworking.",
                PhotoUrl = "https://images.unsplash.com/photo-1486306926a5-20a48f31f04b?w=600&h=400&fit=crop"
            },

            // Pleven Province
            new City
            {
                Name = "Pleven",
                Country = "Bulgaria",
                Latitude = 43.4200,
                Longitude = 24.6150,
                Description = "A major city in northern Bulgaria, historically significant for the Russian-Turkish War.",
                PhotoUrl = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=600&h=400&fit=crop"
            },

            // Plovdiv Province
            new City
            {
                Name = "Plovdiv",
                Country = "Bulgaria",
                Latitude = 42.1500,
                Longitude = 24.7500,
                Description = "Bulgaria's second-largest city with ancient Thracian and Roman heritage.",
                PhotoUrl = "https://images.unsplash.com/photo-1488646953014-85cb44e25828?w=600&h=400&fit=crop"
            },

            // Razgrad Province
            new City
            {
                Name = "Razgrad",
                Country = "Bulgaria",
                Latitude = 43.5306,
                Longitude = 25.9319,
                Description = "A city in northeastern Bulgaria known for its beer production and sports tradition.",
                PhotoUrl = "https://images.unsplash.com/photo-1469854523086-cc02fe5d8800?w=600&h=400&fit=crop"
            },

            // Ruse Province
            new City
            {
                Name = "Ruse",
                Country = "Bulgaria",
                Latitude = 43.8445,
                Longitude = 25.9864,
                Description = "A port city on the Danube River in northern Bulgaria with Austro-Hungarian architecture.",
                PhotoUrl = "https://images.unsplash.com/photo-1476514525535-07fb3b4ae5f1?w=600&h=400&fit=crop"
            },

            // Shumen Province
            new City
            {
                Name = "Shumen",
                Country = "Bulgaria",
                Latitude = 43.2735,
                Longitude = 26.9244,
                Description = "A historic city in northeastern Bulgaria known for its fortress and cultural heritage.",
                PhotoUrl = "https://images.unsplash.com/photo-1495854035989-cebdc8ec6b94?w=600&h=400&fit=crop"
            },

            // Silistra Province
            new City
            {
                Name = "Silistra",
                Country = "Bulgaria",
                Latitude = 44.1206,
                Longitude = 27.2614,
                Description = "A Danube port city in northeastern Bulgaria with Roman and medieval monuments.",
                PhotoUrl = "https://images.unsplash.com/photo-1506905925346-21bda4d32df4?w=600&h=400&fit=crop"
            },

            // Sliven Province
            new City
            {
                Name = "Sliven",
                Country = "Bulgaria",
                Latitude = 42.6756,
                Longitude = 26.3331,
                Description = "A city in eastern Bulgaria known for textile production and the 'Town of Hundred Voivodes'.",
                PhotoUrl = "https://images.unsplash.com/photo-1489749798305-4fea3ba63d60?w=600&h=400&fit=crop"
            },

            // Smolyan Province
            new City
            {
                Name = "Smolyan",
                Country = "Bulgaria",
                Latitude = 41.5754,
                Longitude = 24.6957,
                Description = "A mountain city in southern Bulgaria known for cool climate and skiing.",
                PhotoUrl = "https://images.unsplash.com/photo-1486306926a5-20a48f31f04b?w=600&h=400&fit=crop"
            },

            // Sofia Province (excluding Sofia City itself)
            new City
            {
                Name = "Svoge",
                Country = "Bulgaria",
                Latitude = 42.8931,
                Longitude = 23.0892,
                Description = "A town in Sofia Province known for its mineral water.",
                PhotoUrl = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=600&h=400&fit=crop"
            },

            // Stara Zagora Province
            new City
            {
                Name = "Stara Zagora",
                Country = "Bulgaria",
                Latitude = 42.6342,
                Longitude = 25.6446,
                Description = "A city in south-central Bulgaria with significant sports infrastructure.",
                PhotoUrl = "https://images.unsplash.com/photo-1488646953014-85cb44e25828?w=600&h=400&fit=crop"
            },

            // Targovishte Province
            new City
            {
                Name = "Targovishte",
                Country = "Bulgaria",
                Latitude = 43.2475,
                Longitude = 25.8747,
                Description = "A city in northern Bulgaria with historical and cultural significance.",
                PhotoUrl = "https://images.unsplash.com/photo-1469854523086-cc02fe5d8800?w=600&h=400&fit=crop"
            },

            // Vidin Province
            new City
            {
                Name = "Vidin",
                Country = "Bulgaria",
                Latitude = 43.9963,
                Longitude = 22.8734,
                Description = "A Danube port city in northwestern Bulgaria with well-preserved fortifications.",
                PhotoUrl = "https://images.unsplash.com/photo-1476514525535-07fb3b4ae5f1?w=600&h=400&fit=crop"
            },

            // Yambol Province
            new City
            {
                Name = "Yambol",
                Country = "Bulgaria",
                Latitude = 42.4920,
                Longitude = 26.4858,
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
