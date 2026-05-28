using MongoDB.Driver;
using MongoDB.Bson;
using FixIt.Data.Configuration.Contracts;
using FixIt.Models.Issues;
using FixIt.Models.Common;
using FixIt.Models.Enums;
using FixIt.Models.Locations;
using MongoDB.Driver.GeoJsonObjectModel;

namespace FixIt.Data.Configuration;

public class IssueConfiguration : ICollectionConfigurator
{
    public async Task ConfigureAsync(IMongoDatabase db, bool seedDemoData)
    {
        var issues = db.GetCollection<Issue>("issues");
        var citiesCollection = db.GetCollection<City>("cities");
        var tagsCollection = db.GetCollection<FixIt.Models.Issues.Tag>("tags");

        // Create indexes first — these must exist in every environment.
        await CreateIndexesAsync(issues);

        // Sample issues are demo content (fake "Civic Reporter" author) — never
        // seed them in production.
        if (!seedDemoData)
        {
            Console.WriteLine("[IssueConfiguration] seedDemoData=false. Skipping sample issue seed (indexes still created).");
            return;
        }

        // Only seed if no issues exist yet
        var existingCount = await issues.CountDocumentsAsync(FilterDefinition<Issue>.Empty);
        if (existingCount > 0)
        {
            Console.WriteLine($"[IssueConfiguration] Collection already has {existingCount} documents. Skipping seed.");
            return;
        }

        Console.WriteLine("[IssueConfiguration] Starting to seed sample issues...");

        try
        {
            // Get sample cities
            var sofiaCity = await citiesCollection.Find(Builders<City>.Filter.Eq(c => c.Name, "Sofia")).FirstOrDefaultAsync();
            var plovdivCity = await citiesCollection.Find(Builders<City>.Filter.Eq(c => c.Name, "Plovdiv")).FirstOrDefaultAsync();
            var varnaCity = await citiesCollection.Find(Builders<City>.Filter.Eq(c => c.Name, "Varna")).FirstOrDefaultAsync();
            var burgazCity = await citiesCollection.Find(Builders<City>.Filter.Eq(c => c.Name, "Burgas")).FirstOrDefaultAsync();
            var ruseCity = await citiesCollection.Find(Builders<City>.Filter.Eq(c => c.Name, "Ruse")).FirstOrDefaultAsync();

            // Get sample tags
            var potholeTag = await tagsCollection.Find(Builders<FixIt.Models.Issues.Tag>.Filter.Eq(t => t.Name, "pothole")).FirstOrDefaultAsync();
            var streetLightTag = await tagsCollection.Find(Builders<FixIt.Models.Issues.Tag>.Filter.Eq(t => t.Name, "street-light")).FirstOrDefaultAsync();
            var garbageTag = await tagsCollection.Find(Builders<FixIt.Models.Issues.Tag>.Filter.Eq(t => t.Name, "garbage")).FirstOrDefaultAsync();
            var buildingDamageTag = await tagsCollection.Find(Builders<FixIt.Models.Issues.Tag>.Filter.Eq(t => t.Name, "building-damage")).FirstOrDefaultAsync();
            var treeHazardTag = await tagsCollection.Find(Builders<FixIt.Models.Issues.Tag>.Filter.Eq(t => t.Name, "tree-hazard")).FirstOrDefaultAsync();

            // Check that we have at least the minimum required data
            if (sofiaCity == null || potholeTag == null || garbageTag == null)
            {
                Console.WriteLine("[IssueConfiguration] Missing required city or tag data. Skipping issue seeding.");
                return;
            }

            // Create sample user
            var sampleUser = new UserSummary
            {
                Id = ObjectId.GenerateNewId().ToString(),
                DisplayName = "Civic Reporter",
                AvatarUrl = "https://avatars.dicebear.com/api/avataaars/CivicReporter.svg"
            };

            var sampleIssues = new List<Issue>();

            // Sofia issues
            sampleIssues.AddRange(new[]
            {
                new Issue
                {
                    Title = "Large pothole on Vasil Levski Boulevard",
                    Description = "A significant pothole has appeared on Vasil Levski Boulevard near the National Library. The hole is approximately 1 meter wide and creates a safety hazard for both vehicles and pedestrians. Immediate repair is recommended.",
                    CityId = sofiaCity.Id,
                    Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                        new GeoJson2DGeographicCoordinates(23.3219, 42.6977)
                    ),
                    Address = "Vasil Levski Boulevard, Sofia",
                    Reporter = sampleUser,
                    Status = IssueStatus.Confirmed,
                    Priority = IssuePriority.High,
                    TagIds = new HashSet<string> { potholeTag.Id },
                    Upvotes = 12,
                    Downvotes = 0,
                    CreatedAt = DateTime.UtcNow.AddDays(-5)
                },
                new Issue
                {
                    Title = "Street light broken on Alexander Nevsky Cathedral square",
                    Description = "The street light on the eastern side of Alexander Nevsky Cathedral square is not functioning. This creates poor lighting conditions in the evenings and poses safety risks.",
                    CityId = sofiaCity.Id,
                    Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                        new GeoJson2DGeographicCoordinates(23.3262, 42.6976)
                    ),
                    Address = "Alexander Nevsky Cathedral, Sofia",
                    Reporter = sampleUser,
                    Status = IssueStatus.Confirmed,
                    Priority = IssuePriority.Medium,
                    TagIds = streetLightTag != null ? new HashSet<string> { streetLightTag.Id } : new HashSet<string>(),
                    Upvotes = 8,
                    Downvotes = 0,
                    CreatedAt = DateTime.UtcNow.AddDays(-3)
                },
                new Issue
                {
                    Title = "Excessive garbage accumulation near City Garden",
                    Description = "Large amounts of trash and litter have accumulated near the City Garden entrance. The trash bins are overflowing and garbage is scattered around the area. Urgent cleanup needed.",
                    CityId = sofiaCity.Id,
                    Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                        new GeoJson2DGeographicCoordinates(23.3262, 42.6959)
                    ),
                    Address = "City Garden, Sofia",
                    Reporter = sampleUser,
                    Status = IssueStatus.Confirmed,
                    Priority = IssuePriority.High,
                    TagIds = new HashSet<string> { garbageTag.Id },
                    Upvotes = 15,
                    Downvotes = 1,
                    CreatedAt = DateTime.UtcNow.AddDays(-2)
                },
                new Issue
                {
                    Title = "Building facade plaster falling on Women's Bazaar street",
                    Description = "Loose plaster from a historic building facade is falling on Women's Bazaar street, creating a safety hazard for pedestrians below. The building appears to be abandoned and needs immediate stabilization.",
                    CityId = sofiaCity.Id,
                    Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                        new GeoJson2DGeographicCoordinates(23.3234, 42.6976)
                    ),
                    Address = "Women's Bazaar, Sofia",
                    Reporter = sampleUser,
                    Status = IssueStatus.InProgress,
                    Priority = IssuePriority.Critical,
                    TagIds = buildingDamageTag != null ? new HashSet<string> { buildingDamageTag.Id } : new HashSet<string>(),
                    Upvotes = 20,
                    Downvotes = 0,
                    CreatedAt = DateTime.UtcNow.AddDays(-7)
                }
            });

            // Plovdiv issues
            if (plovdivCity != null)
            {
                sampleIssues.AddRange(new[]
                {
                    new Issue
                    {
                        Title = "Damaged sidewalk on Aleksandar Batenberg Street",
                        Description = "The sidewalk on Aleksandar Batenberg Street has several broken tiles and raised sections creating tripping hazards. Elderly citizens and people with mobility issues are particularly affected.",
                        CityId = plovdivCity.Id,
                        Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                            new GeoJson2DGeographicCoordinates(24.7505, 42.1444)
                        ),
                        Address = "Aleksandar Batenberg Street, Plovdiv",
                        Reporter = sampleUser,
                        Status = IssueStatus.Confirmed,
                        Priority = IssuePriority.Medium,
                        TagIds = new HashSet<string> { potholeTag.Id },
                        Upvotes = 7,
                        Downvotes = 0,
                        CreatedAt = DateTime.UtcNow.AddDays(-4)
                    },
                    new Issue
                    {
                        Title = "Dangerous tree branch hanging over sidewalk on Main Street",
                        Description = "A large tree branch on Main Street is hanging dangerously low and could fall at any moment, especially during windy weather. This poses a serious safety risk to pedestrians.",
                        CityId = plovdivCity.Id,
                        Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                            new GeoJson2DGeographicCoordinates(24.7506, 42.1449)
                        ),
                        Address = "Main Street, Plovdiv",
                        Reporter = sampleUser,
                        Status = IssueStatus.Confirmed,
                        Priority = IssuePriority.High,
                        TagIds = treeHazardTag != null ? new HashSet<string> { treeHazardTag.Id } : new HashSet<string>(),
                        Upvotes = 11,
                        Downvotes = 0,
                        CreatedAt = DateTime.UtcNow.AddDays(-6)
                    }
                });
            }

            // Varna issues
            if (varnaCity != null)
            {
                sampleIssues.Add(new Issue
                {
                    Title = "Broken street light on Sea Garden beachfront",
                    Description = "A street light on the Sea Garden beachfront is completely non-functional. The entire area remains dark at night, discouraging tourism and creating safety concerns.",
                    CityId = varnaCity.Id,
                    Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                        new GeoJson2DGeographicCoordinates(28.1578, 43.2015)
                    ),
                    Address = "Sea Garden, Varna",
                    Reporter = sampleUser,
                    Status = IssueStatus.Confirmed,
                    Priority = IssuePriority.Medium,
                    TagIds = streetLightTag != null ? new HashSet<string> { streetLightTag.Id } : new HashSet<string>(),
                    Upvotes = 9,
                    Downvotes = 0,
                    CreatedAt = DateTime.UtcNow.AddDays(-8)
                });
            }

            // Burgas issues
            if (burgazCity != null)
            {
                sampleIssues.Add(new Issue
                {
                    Title = "Multiple potholes on Aleksander Batenberg Avenue",
                    Description = "Several significant potholes have appeared on Aleksander Batenberg Avenue after recent heavy rains. Vehicles are being damaged by these road hazards.",
                    CityId = burgazCity.Id,
                    Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                        new GeoJson2DGeographicCoordinates(27.4711, 42.5047)
                    ),
                    Address = "Aleksander Batenberg Avenue, Burgas",
                    Reporter = sampleUser,
                    Status = IssueStatus.Confirmed,
                    Priority = IssuePriority.High,
                    TagIds = new HashSet<string> { potholeTag.Id },
                    Upvotes = 13,
                    Downvotes = 0,
                    CreatedAt = DateTime.UtcNow.AddDays(-9)
                });
            }

            // Ruse issues
            if (ruseCity != null)
            {
                sampleIssues.Add(new Issue
                {
                    Title = "Graffiti and garbage near Danube waterfront",
                    Description = "The Danube waterfront area near the port has become covered with graffiti and excessive garbage. This damages the city's appearance and attracts more vandalism.",
                    CityId = ruseCity.Id,
                    Location = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(
                        new GeoJson2DGeographicCoordinates(25.4383, 43.8510)
                    ),
                    Address = "Danube Waterfront, Ruse",
                    Reporter = sampleUser,
                    Status = IssueStatus.Confirmed,
                    Priority = IssuePriority.Medium,
                    TagIds = new HashSet<string> { garbageTag.Id },
                    Upvotes = 6,
                    Downvotes = 0,
                    CreatedAt = DateTime.UtcNow.AddDays(-10)
                });
            }

            // Insert all sample issues
            if (sampleIssues.Count > 0)
            {
                await issues.InsertManyAsync(sampleIssues);
                Console.WriteLine($"[IssueConfiguration] Successfully seeded {sampleIssues.Count} sample issues.");
            }
            else
            {
                Console.WriteLine("[IssueConfiguration] No sample issues were created.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IssueConfiguration] Error seeding issues: {ex.Message}");
        }
    }

    private async Task CreateIndexesAsync(IMongoCollection<Issue> issues)
    {
        try
        {
            // Geospatial index for "find issues near me"
            var geoIndex = new CreateIndexModel<Issue>(
                Builders<Issue>.IndexKeys.Geo2DSphere(i => i.Location),
                new CreateIndexOptions { Name = "idx_issues_location_2dsphere" }
            );
            await issues.Indexes.CreateOneAsync(geoIndex);

            // Text search index for title and description
            var textIndex = new CreateIndexModel<Issue>(
                Builders<Issue>.IndexKeys
                    .Text(i => i.Title)
                    .Text(i => i.Description),
                new CreateIndexOptions { Name = "idx_issues_text" }
            );
            await issues.Indexes.CreateOneAsync(textIndex);

            // Compound index for common queries
            var cityStatusIndex = new CreateIndexModel<Issue>(
                Builders<Issue>.IndexKeys
                    .Ascending(i => i.CityId)
                    .Ascending(i => i.Status)
                    .Descending(i => i.CreatedAt),
                new CreateIndexOptions { Name = "idx_issues_city_status_created" }
            );
            await issues.Indexes.CreateOneAsync(cityStatusIndex);

            // Index for user's issues
            var reporterIndex = new CreateIndexModel<Issue>(
                Builders<Issue>.IndexKeys.Ascending("Reporter.Id"),
                new CreateIndexOptions { Name = "idx_issues_reporter_id" }
            );
            await issues.Indexes.CreateOneAsync(reporterIndex);

            // Partial index for active issues
            var command = new BsonDocument
            {
                { "createIndexes", "issues" },
                { "indexes", new BsonArray
                    {
                        new BsonDocument
                        {
                            { "name", "idx_issues_active" },
                            { "key", new BsonDocument("CreatedAt", -1) },
                            { "partialFilterExpression", new BsonDocument("IsDeleted", false) }
                        }
                    }
                }
            };
            await issues.Database.RunCommandAsync<BsonDocument>(command);

            Console.WriteLine("[IssueConfiguration] Indexes created successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[IssueConfiguration] Error creating indexes: {ex.Message}");
        }
    }
}
