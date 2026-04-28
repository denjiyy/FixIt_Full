using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using System.Reflection;

namespace FixIt.Data.Infrastructure.Migrations;

/// <summary>
/// Manages database migrations
/// Discovers and runs migrations in version order
/// Maintains migration history for auditability
/// </summary>
public class MigrationRunner
{
    private readonly IMongoDatabase _database;
    private readonly ILogger<MigrationRunner> _logger;
    private const string MigrationCollectionName = "_migrations";

    public MigrationRunner(IMongoDatabase database, ILogger<MigrationRunner> logger)
    {
        _database = database;
        _logger = logger;
    }

    /// <summary>
    /// Discover and execute all pending migrations
    /// Safe to call multiple times - tracks which migrations have run
    /// </summary>
    public async Task RunPendingMigrationsAsync()
    {
        try
        {
            // Ensure migration tracking collection exists
            await EnsureMigrationCollectionAsync();

            // Discover all migrations
            var migrations = DiscoverMigrations();

            if (migrations.Count == 0)
            {
                _logger.LogInformation("No migrations found");
                return;
            }

            // Get already-applied migrations
            var appliedVersions = await GetAppliedMigrationsAsync();

            // Filter to pending migrations (not yet applied)
            var pendingMigrations = migrations
                .Where(m => !appliedVersions.Contains(m.Version))
                .OrderBy(m => m.Version)
                .ToList();

            if (pendingMigrations.Count == 0)
            {
                _logger.LogInformation("No pending migrations to run");
                return;
            }

            _logger.LogInformation("Found {PendingCount} pending migration(s)", pendingMigrations.Count);

            // Execute each migration in order
            foreach (var migration in pendingMigrations)
            {
                await ExecuteMigrationAsync(migration);
            }

            _logger.LogInformation("All migrations completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration execution failed");
            throw;
        }
    }

    /// <summary>
    /// Get migrations applied after a specific date
    /// Useful for diagnostics and rollback planning
    /// </summary>
    public async Task<IList<MigrationRecord>> GetMigrationHistoryAsync(DateTime? since = null)
    {
        var collection = _database.GetCollection<MigrationRecord>(MigrationCollectionName);
        var filter = since.HasValue 
            ? Builders<MigrationRecord>.Filter.Gte(m => m.AppliedAt, since.Value)
            : Builders<MigrationRecord>.Filter.Empty;

        var history = await collection
            .Find(filter)
            .SortBy(m => m.AppliedAt)
            .ToListAsync();

        return history;
    }

    private List<IMigration> DiscoverMigrations()
    {
        var migrations = new List<IMigration>();

        try
        {
            // Find all classes implementing IMigration in this assembly
            var migrationType = typeof(IMigration);
            var migrationTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => migrationType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .ToList();

            // Instantiate each migration
            foreach (var type in migrationTypes)
            {
                try
                {
                    var instance = Activator.CreateInstance(type) as IMigration;
                    if (instance != null)
                    {
                        migrations.Add(instance);
                        _logger.LogDebug("Discovered migration: {Version} - {Description}", instance.Version, instance.Description);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to instantiate migration {Type}", type.Name);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error discovering migrations");
        }

        return migrations;
    }

    private async Task<HashSet<string>> GetAppliedMigrationsAsync()
    {
        try
        {
            var collection = _database.GetCollection<MigrationRecord>(MigrationCollectionName);
            var records = await collection
                .Find(m => m.Success)
                .Project(m => m.Version)
                .ToListAsync();

            return new HashSet<string>(records);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving applied migrations");
            return new HashSet<string>();
        }
    }

    private async Task ExecuteMigrationAsync(IMigration migration)
    {
        var collection = _database.GetCollection<MigrationRecord>(MigrationCollectionName);
        var record = new MigrationRecord
        {
            Version = migration.Version,
            Description = migration.Description,
            AppliedAt = DateTime.UtcNow,
            Success = false
        };

        try
        {
            _logger.LogInformation("Running migration {Version}: {Description}", migration.Version, migration.Description);

            // Execute the migration
            await migration.UpAsync(_database);

            // Mark as successful
            record.Success = true;
            await collection.InsertOneAsync(record);

            _logger.LogInformation("✓ Migration {Version} completed successfully", migration.Version);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "✗ Migration {Version} failed", migration.Version);

            record.Success = false;
            record.Error = ex.Message;
            await collection.InsertOneAsync(record);

            throw;
        }
    }

    private async Task EnsureMigrationCollectionAsync()
    {
        try
        {
            var collectionNames = await _database.ListCollectionNamesAsync();
            var collections = await collectionNames.ToListAsync();

            if (!collections.Contains(MigrationCollectionName))
            {
                await _database.CreateCollectionAsync(MigrationCollectionName);
                
                // Create index on Version for quick lookups
                var collection = _database.GetCollection<MigrationRecord>(MigrationCollectionName);
                var indexModel = new CreateIndexModel<MigrationRecord>(
                    Builders<MigrationRecord>.IndexKeys.Ascending(m => m.Version),
                    new CreateIndexOptions { Unique = true }
                );
                await collection.Indexes.CreateOneAsync(indexModel);
            }
        }
        catch (MongoCommandException ex) when (ex.Code == 48) // NamespaceExists
        {
            // Collection already exists, ignore
        }
    }
}
