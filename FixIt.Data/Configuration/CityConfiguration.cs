using MongoDB.Driver;
using FixIt.Data.Configuration.Contracts;
using FixIt.Models.Locations;

namespace FixIt.Data.Configuration;

public class CityConfiguration : ICollectionConfigurator
{
    public async Task ConfigureAsync(IMongoDatabase db, bool seedDemoData)
    {
        // Cities are reference data: the issue-reporting flow requires at least
        // one city to exist, so we seed everywhere (including prod) — not gated
        // by seedDemoData.
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
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c0/Catedral_de_Alejandro_Nevski_--_2019_--_Sof%C3%ADa%2C_Bulgaria.jpg/1280px-Catedral_de_Alejandro_Nevski_--_2019_--_Sof%C3%ADa%2C_Bulgaria.jpg"
            },

            // Blagoevgrad Province
            new City
            {
                Name = "Blagoevgrad",
                Country = "Bulgaria",
                Latitude = 42.016707,
                Longitude = 23.094362,
                Description = "A city in southwestern Bulgaria, known as a cultural center with significant youth population.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/3/30/%D0%91%D0%BB%D0%B0%D0%B3%D0%BE%D0%B5%D0%B2%D0%B3%D1%80%D0%B0%D0%B4_-_panoramio_%2826%29.jpg/1280px-%D0%91%D0%BB%D0%B0%D0%B3%D0%BE%D0%B5%D0%B2%D0%B3%D1%80%D0%B0%D0%B4_-_panoramio_%2826%29.jpg"
            },

            // Burgas Province
            new City
            {
                Name = "Burgas",
                Country = "Bulgaria",
                Latitude = 42.5047,
                Longitude = 27.4711,
                Description = "A major Black Sea port city and beach destination in southeastern Bulgaria.",
                PhotoUrl = "https://dynamic-media-cdn.tripadvisor.com/media/photo-o/08/19/fe/30/getlstd-property-photo.jpg?w=1200&h=-1&s=1"
            },

            // Varna Province
            new City
            {
                Name = "Varna",
                Country = "Bulgaria",
                Latitude = 43.2141,
                Longitude = 27.9147,
                Description = "Bulgaria's premier seaside resort city located on the Black Sea coast.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/en/thumb/7/79/Dramatheatrevarna.jpg/1280px-Dramatheatrevarna.jpg"
            },

            // Dobrich Province
            new City
            {
                Name = "Dobrich",
                Country = "Bulgaria",
                Latitude = 43.5704,
                Longitude = 28.7626,
                Description = "A city in northeastern Bulgaria known for its wheat production and agricultural heritage.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/1/18/Dobrich_Sunrise%2C_Winter_2014.JPG/1280px-Dobrich_Sunrise%2C_Winter_2014.JPG"
            },

            // Gabrovo Province
            new City
            {
                Name = "Gabrovo",
                Country = "Bulgaria",
                Latitude = 43.2744,
                Longitude = 24.8639,
                Description = "Located in central Bulgaria, famous for its textile industry and 'Joke Festival'.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c0/TownHall_Gabrovo.jpg/1280px-TownHall_Gabrovo.jpg"
            },

            // Haskovo Province
            new City
            {
                Name = "Haskovo",
                Country = "Bulgaria",
                Latitude = 41.9294,
                Longitude = 25.5500,
                Description = "A city in southern Bulgaria situated on the Maritsa River.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/c/cb/Haskovo_Bulgaria_cityscape.jpg/1280px-Haskovo_Bulgaria_cityscape.jpg"
            },

            // Kardjali Province
            new City
            {
                Name = "Kardzhali",
                Country = "Bulgaria",
                Latitude = 41.6478,
                Longitude = 25.4185,
                Description = "A city in southern Bulgaria known for its Thracian heritage and diamond reserves.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/5/5d/%D0%98%D1%81%D1%82%D0%BE%D1%80%D0%B8%D1%87%D0%B5%D1%81%D0%BA%D0%B8%D1%8F%D1%82_%D0%BC%D1%83%D0%B7%D0%B5%D0%B9_%D0%B2_%D0%9A%D1%8A%D1%80%D0%B4%D0%B6%D0%B0%D0%BB%D0%B8.JPG/1280px-%D0%98%D1%81%D1%82%D0%BE%D1%80%D0%B8%D1%87%D0%B5%D1%81%D0%BA%D0%B8%D1%8F%D1%82_%D0%BC%D1%83%D0%B7%D0%B5%D0%B9_%D0%B2_%D0%9A%D1%8A%D1%80%D0%B4%D0%B6%D0%B0%D0%BB%D0%B8.JPG"
            },

            // Kyustendil Province
            new City
            {
                Name = "Kyustendil",
                Country = "Bulgaria",
                Latitude = 42.2842,
                Longitude = 22.6951,
                Description = "A city in southwestern Bulgaria known for its natural hot springs and wine production.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/0/09/Kyustendil_25.jpg/1024px-Kyustendil_25.jpg"
            },

            // Lovech Province
            new City
            {
                Name = "Lovech",
                Country = "Bulgaria",
                Latitude = 43.1382,
                Longitude = 24.7202,
                Description = "A historic city in north-central Bulgaria known for its old town and medieval bridge.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/3/3b/Bulgaria-Lovech-03.jpg/1024px-Bulgaria-Lovech-03.jpg"
            },

            // Montana Province
            new City
            {
                Name = "Montana",
                Country = "Bulgaria",
                Latitude = 43.4097,
                Longitude = 23.2261,
                Description = "A city in northwestern Bulgaria with rich Roman and medieval heritage.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/4/47/Montana-downtown.jpg/1280px-Montana-downtown.jpg"
            },

            // Pazardzhik Province
            new City
            {
                Name = "Pazardzhik",
                Country = "Bulgaria",
                Latitude = 42.1934,
                Longitude = 24.3281,
                Description = "Located in south-central Bulgaria, known for rose production and textile industry.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/8/88/Pazardzhik_City_Centre.jpg/1024px-Pazardzhik_City_Centre.jpg"
            },

            // Pernik Province
            new City
            {
                Name = "Pernik",
                Country = "Bulgaria",
                Latitude = 42.6042,
                Longitude = 22.9619,
                Description = "An industrial city near Sofia, known for coal mining and metalworking.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/e/ec/Pernik-culture-palace-left.jpg/1920px-Pernik-culture-palace-left.jpg"
            },

            // Pleven Province
            new City
            {
                Name = "Pleven",
                Country = "Bulgaria",
                Latitude = 43.4200,
                Longitude = 24.6150,
                Description = "A major city in northern Bulgaria, historically significant for the Russian-Turkish War.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/9/91/%D0%9F%D0%BB%D0%B5%D0%B2%D0%B5%D0%BD_%D0%BC%D0%B0%D1%80%D1%82_2014_-_panoramio_%281%29.jpg/1280px-%D0%9F%D0%BB%D0%B5%D0%B2%D0%B5%D0%BD_%D0%BC%D0%B0%D1%80%D1%82_2014_-_panoramio_%281%29.jpg"
            },

            // Plovdiv Province
            new City
            {
                Name = "Plovdiv",
                Country = "Bulgaria",
                Latitude = 42.1500,
                Longitude = 24.7500,
                Description = "Bulgaria's second-largest city with ancient Thracian and Roman heritage.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/0/0c/Bulgaria_Bulgaria-0785_-_Roman_Theatre_of_Philippopolis_%287432772486%29.jpg/1280px-Bulgaria_Bulgaria-0785_-_Roman_Theatre_of_Philippopolis_%287432772486%29.jpg"
            },

            // Razgrad Province
            new City
            {
                Name = "Razgrad",
                Country = "Bulgaria",
                Latitude = 43.5306,
                Longitude = 25.9319,
                Description = "A city in northeastern Bulgaria known for its beer production and sports tradition.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/d/d4/%D0%95%D1%82%D0%BD%D0%BE%D0%B3%D1%80%D0%B0%D1%84%D1%81%D0%BA%D0%B8_%D0%BC%D1%83%D0%B7%D0%B5%D0%B9_%D0%B2_%D0%B3%D1%80%D0%B0%D0%B4_%D0%A0%D0%B0%D0%B7%D0%B3%D1%80%D0%B0%D0%B4.jpg/1280px-%D0%95%D1%82%D0%BD%D0%BE%D0%B3%D1%80%D0%B0%D1%84%D1%81%D0%BA%D0%B8_%D0%BC%D1%83%D0%B7%D0%B5%D0%B9_%D0%B2_%D0%B3%D1%80%D0%B0%D0%B4_%D0%A0%D0%B0%D0%B7%D0%B3%D1%80%D0%B0%D0%B4.jpg"
            },

            // Ruse Province
            new City
            {
                Name = "Ruse",
                Country = "Bulgaria",
                Latitude = 43.8445,
                Longitude = 25.9864,
                Description = "A port city on the Danube River in northern Bulgaria with Austro-Hungarian architecture.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/3/39/%D0%9E%D0%BF%D0%B5%D1%80%D0%B0%D1%82%D0%B0_%D0%B2_%D0%A0%D1%83%D1%81%D0%B5.jpg/1280px-%D0%9E%D0%BF%D0%B5%D1%80%D0%B0%D1%82%D0%B0_%D0%B2_%D0%A0%D1%83%D1%81%D0%B5.jpg"
            },

            // Shumen Province
            new City
            {
                Name = "Shumen",
                Country = "Bulgaria",
                Latitude = 43.2735,
                Longitude = 26.9244,
                Description = "A historic city in northeastern Bulgaria known for its fortress and cultural heritage.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/5/51/Shumen_chitalishte_Dobri_Voynikov.jpg/1280px-Shumen_chitalishte_Dobri_Voynikov.jpg"
            },

            // Silistra Province
            new City
            {
                Name = "Silistra",
                Country = "Bulgaria",
                Latitude = 44.1206,
                Longitude = 27.2614,
                Description = "A Danube port city in northeastern Bulgaria with Roman and medieval monuments.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/c/c9/Silistra-art-gallery-Minkov.jpg/1024px-Silistra-art-gallery-Minkov.jpg"
            },

            // Sliven Province
            new City
            {
                Name = "Sliven",
                Country = "Bulgaria",
                Latitude = 42.6756,
                Longitude = 26.3331,
                Description = "A city in eastern Bulgaria known for textile production and the 'Town of Hundred Voivodes'.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/9/99/Municipality_of_Sliven_Photo.jpg/1280px-Municipality_of_Sliven_Photo.jpg"
            },

            // Smolyan Province
            new City
            {
                Name = "Smolyan",
                Country = "Bulgaria",
                Latitude = 41.5754,
                Longitude = 24.6957,
                Description = "A mountain city in southern Bulgaria known for cool climate and skiing.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/e/e0/%D0%A1%D0%BC%D0%BE%D0%BB%D1%8F%D0%BD_2691396959_f63b323fab_o.jpg/1024px-%D0%A1%D0%BC%D0%BE%D0%BB%D1%8F%D0%BD_2691396959_f63b323fab_o.jpg"
            },

            // Stara Zagora Province
            new City
            {
                Name = "Stara Zagora",
                Country = "Bulgaria",
                Latitude = 42.6342,
                Longitude = 25.6446,
                Description = "A city in south-central Bulgaria with significant sports infrastructure.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/3/33/Samarsko_Zname_Panorama.jpg/1280px-Samarsko_Zname_Panorama.jpg"
            },

            // Targovishte Province
            new City
            {
                Name = "Targovishte",
                Country = "Bulgaria",
                Latitude = 43.2475,
                Longitude = 25.8747,
                Description = "A city in northern Bulgaria with historical and cultural significance.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/5/5c/Targovishte-MainSquare.jpg/1280px-Targovishte-MainSquare.jpg"
            },

            // Veliko Tarnovo Province
            new City
            {
                Name = "Veliko Tarnovo",
                Country = "Bulgaria",
                Latitude = 43.1969,
                Longitude = 25.1449,
                Description = "A historic city in north-central Bulgaria known for its medieval fortress and architecture.",
                PhotoUrl = "https://traventuria.com/wp-content/uploads/2016/10/veliko-tarnovo-1.jpg"
            },

            // Vidin Province
            new City
            {
                Name = "Vidin",
                Country = "Bulgaria",
                Latitude = 43.9963,
                Longitude = 22.8734,
                Description = "A Danube port city in northwestern Bulgaria with well-preserved fortifications.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/7/70/Theater_House_in_Vidin_%2827460729905%29.jpg/1280px-Theater_House_in_Vidin_%2827460729905%29.jpg"
            },

            // Vratsa Province
            new City
            {
                Name = "Vratsa",
                Country = "Bulgaria",
                Latitude = 43.2339,
                Longitude = 23.5686,
                Description = "A city in northwestern Bulgaria surrounded by scenic mountain ranges.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/a/a4/Vratsa_12.jpg/1024px-Vratsa_12.jpg"
            },

            // Yambol Province
            new City
            {
                Name = "Yambol",
                Country = "Bulgaria",
                Latitude = 42.4920,
                Longitude = 26.4858,
                Description = "A city in southeastern Bulgaria with ancient Thracian ruins nearby.",
                PhotoUrl = "https://upload.wikimedia.org/wikipedia/commons/thumb/9/9d/YAMBOL_new_center.jpg/1920px-YAMBOL_new_center.jpg"
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
