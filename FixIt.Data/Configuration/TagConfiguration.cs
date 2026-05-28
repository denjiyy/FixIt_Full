using MongoDB.Driver;
using FixIt.Data.Configuration.Contracts;

namespace FixIt.Data.Configuration;

public class TagConfiguration : ICollectionConfigurator
{
    public async Task ConfigureAsync(IMongoDatabase db, bool seedDemoData)
    {
        // Tags are reference data: the report-issue picker depends on this
        // taxonomy, so we seed everywhere (including prod) — not gated by
        // seedDemoData.
        var tags = db.GetCollection<FixIt.Models.Issues.Tag>("tags");

        // Create indexes
        await CreateIndexesAsync(tags);

        // Check if tags already exist
        var existingCount = await tags.CountDocumentsAsync(FilterDefinition<FixIt.Models.Issues.Tag>.Empty);
        if (existingCount > 0)
        {
            Console.WriteLine($"[TagConfiguration] Collection already has {existingCount} documents. Skipping seed.");
            return;
        }

        Console.WriteLine("[TagConfiguration] Starting to seed tags...");

        var seed = new[]
        {
            // Roads & Transportation
            new FixIt.Models.Issues.Tag
            {
                Name = "pothole",
                Category = "Roads",
                UsageCount = 0,
                IsApproved = true,
                Description = "Damaged road surface with holes and cracks"
            },
            new FixIt.Models.Issues.Tag
            {
                Name = "broken-road",
                Category = "Roads",
                UsageCount = 0,
                IsApproved = true,
                Description = "Road surface deterioration and damage"
            },
            new FixIt.Models.Issues.Tag
            {
                Name = "sidewalk-damage",
                Category = "Roads",
                UsageCount = 0,
                IsApproved = true,
                Description = "Damaged or broken sidewalk/pedestrian paths"
            },
            new FixIt.Models.Issues.Tag
            {
                Name = "road-marking",
                Category = "Roads",
                UsageCount = 0,
                IsApproved = true,
                Description = "Faded or missing road markings and lanes"
            },
            new FixIt.Models.Issues.Tag
            {
                Name = "traffic-light",
                Category = "Roads",
                UsageCount = 0,
                IsApproved = true,
                Description = "Malfunctioning or broken traffic lights"
            },

            // Lighting
            new FixIt.Models.Issues.Tag
            {
                Name = "street-light",
                Category = "Lighting",
                UsageCount = 0,
                IsApproved = true,
                Description = "Broken or non-functional street lighting"
            },
            new FixIt.Models.Issues.Tag
            {
                Name = "dim-lighting",
                Category = "Lighting",
                UsageCount = 0,
                IsApproved = true,
                Description = "Inadequate street lighting in the area"
            },
            new FixIt.Models.Issues.Tag
            {
                Name = "missing-lamp",
                Category = "Lighting",
                UsageCount = 0,
                IsApproved = true,
                Description = "Missing or removed street lamp"
            },

            // Sanitation & Cleanliness
            new FixIt.Models.Issues.Tag
            {
                Name = "garbage",
                Category = "Sanitation",
                UsageCount = 0,
                IsApproved = true,
                Description = "Excessive garbage and litter in the area"
            },
            new FixIt.Models.Issues.Tag
            {
                Name = "illegal-dump",
                Category = "Sanitation",
                UsageCount = 0,
                IsApproved = true,
                Description = "Illegal dumping of waste materials"
            },
            new FixIt.Models.Issues.Tag
            {
                Name = "graffiti",
                Category = "Sanitation",
                UsageCount = 0,
                IsApproved = true,
                Description = "Vandalism and graffiti on public property"
            },
            new FixIt.Models.Issues.Tag
            {
                Name = "trash-bin",
                Category = "Sanitation",
                UsageCount = 0,
                IsApproved = true,
                Description = "Overflowing or missing trash bins"
            },

            // Parks & Greenery
            new FixIt.Models.Issues.Tag
            {
                Name = "park-maintenance",
                Category = "Parks",
                UsageCount = 0,
                IsApproved = true,
                Description = "Park requires maintenance and cleaning"
            },
            new FixIt.Models.Issues.Tag
            {
                Name = "broken-bench",
                Category = "Parks",
                UsageCount = 0,
                IsApproved = true,
                Description = "Damaged or broken park benches"
            },
            new FixIt.Models.Issues.Tag
            {
                Name = "playground",
                Category = "Parks",
                UsageCount = 0,
                IsApproved = true,
                Description = "Playground equipment damage or safety issues"
            },
            new FixIt.Models.Issues.Tag
            {
                Name = "tree-hazard",
                Category = "Parks",
                UsageCount = 0,
                IsApproved = true,
                Description = "Dangerous tree branches or fallen trees"
            },

            // Public Utilities
            new FixIt.Models.Issues.Tag
            {
                Name = "water-leak",
                Category = "Utilities",
                UsageCount = 0,
                IsApproved = true,
                Description = "Water pipe leak or broken water line"
            },
            new FixIt.Models.Issues.Tag
            {
                Name = "sewer-issue",
                Category = "Utilities",
                UsageCount = 0,
                IsApproved = true,
                Description = "Sewer system problem or backup"
            },
            new FixIt.Models.Issues.Tag
            {
                Name = "power-outage",
                Category = "Utilities",
                UsageCount = 0,
                IsApproved = true,
                Description = "Electrical power line issue or outage"
            },
            new FixIt.Models.Issues.Tag
            {
                Name = "gas-leak",
                Category = "Utilities",
                UsageCount = 0,
                IsApproved = true,
                Description = "Gas leak or hazardous gas detection"
            },

            // Building & Infrastructure
            new FixIt.Models.Issues.Tag
            {
                Name = "building-damage",
                Category = "Infrastructure",
                UsageCount = 0,
                IsApproved = true,
                Description = "Damaged building facade or structure"
            },
            new FixIt.Models.Issues.Tag
            {
                Name = "loose-plaster",
                Category = "Infrastructure",
                UsageCount = 0,
                IsApproved = true,
                Description = "Loose or falling building plaster/concrete"
            },
            new FixIt.Models.Issues.Tag
            {
                Name = "dangerous-structure",
                Category = "Infrastructure",
                UsageCount = 0,
                IsApproved = true,
                Description = "Structurally unsound or dangerous building"
            },

            // Public Safety
            new FixIt.Models.Issues.Tag
            {
                Name = "safety-hazard",
                Category = "Safety",
                UsageCount = 0,
                IsApproved = true,
                Description = "General public safety hazard"
            },
            new FixIt.Models.Issues.Tag
            {
                Name = "missing-barrier",
                Category = "Safety",
                UsageCount = 0,
                IsApproved = true,
                Description = "Missing safety barriers or guardrails"
            },
            new FixIt.Models.Issues.Tag
            {
                Name = "construction-hazard",
                Category = "Safety",
                UsageCount = 0,
                IsApproved = true,
                Description = "Unsafe construction site or temporary hazard"
            },

            // Air & Noise Pollution
            new FixIt.Models.Issues.Tag
            {
                Name = "air-pollution",
                Category = "Environment",
                UsageCount = 0,
                IsApproved = true,
                Description = "Air quality issues or excessive pollution"
            },
            new FixIt.Models.Issues.Tag
            {
                Name = "noise-pollution",
                Category = "Environment",
                UsageCount = 0,
                IsApproved = true,
                Description = "Excessive noise from various sources"
            },

            // Services
            new FixIt.Models.Issues.Tag
            {
                Name = "transit-issue",
                Category = "Services",
                UsageCount = 0,
                IsApproved = true,
                Description = "Public transportation problem or service interruption"
            },
            new FixIt.Models.Issues.Tag
            {
                Name = "bus-shelter",
                Category = "Services",
                UsageCount = 0,
                IsApproved = true,
                Description = "Damaged bus shelter or missing infrastructure"
            }
        };

        await tags.InsertManyAsync(seed);
        Console.WriteLine($"[TagConfiguration] Successfully seeded {seed.Length} tags.");
    }

    private async Task CreateIndexesAsync(IMongoCollection<FixIt.Models.Issues.Tag> tags)
    {
        try
        {
            var uniqueIndex = new CreateIndexModel<FixIt.Models.Issues.Tag>(
                Builders<FixIt.Models.Issues.Tag>.IndexKeys.Ascending(t => t.Name),
                new CreateIndexOptions { Unique = true, Name = "ux_tags_name" }
            );
            await tags.Indexes.CreateOneAsync(uniqueIndex);

            var usageIndex = new CreateIndexModel<FixIt.Models.Issues.Tag>(
                Builders<FixIt.Models.Issues.Tag>.IndexKeys.Descending(t => t.UsageCount),
                new CreateIndexOptions { Name = "ix_tags_usage" }
            );
            await tags.Indexes.CreateOneAsync(usageIndex);

            var categoryIndex = new CreateIndexModel<FixIt.Models.Issues.Tag>(
                Builders<FixIt.Models.Issues.Tag>.IndexKeys.Ascending(t => t.Category),
                new CreateIndexOptions { Name = "ix_tags_category" }
            );
            await tags.Indexes.CreateOneAsync(categoryIndex);

            Console.WriteLine("[TagConfiguration] Indexes created successfully.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TagConfiguration] Error creating indexes: {ex.Message}");
        }
    }
}
