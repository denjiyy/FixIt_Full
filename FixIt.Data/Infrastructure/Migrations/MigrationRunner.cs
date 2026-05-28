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
    private const string LockCollectionName = "_migration_lock";
    // Stale lock cleanup: assume any lock older than this is from a crashed
    // previous run. Long enough that a legitimately slow migration won't be
    // stolen out from under itself; short enough that a crashed pod doesn't
    // wedge the next deploy indefinitely.
    private static readonly TimeSpan LockTtl = TimeSpan.FromMinutes(10);
    // Bounded wait when another instance is already migrating. Rolling deploys
    // typically finish in seconds; we'd rather time out and crash loudly than
    // silently skip migrations because the loser of the race gave up early.
    private static readonly TimeSpan LockAcquireTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LockPollInterval = TimeSpan.FromSeconds(2);

    public MigrationRunner(IMongoDatabase database, ILogger<MigrationRunner> logger)
    {
        _database = database;
        _logger = logger;
    }

    /// <summary>
    /// Discover and execute all pending migrations
    /// Safe to call multiple times - tracks which migrations have run.
    /// Coordinates across concurrent processes via a Mongo-backed advisory lock
    /// so rolling deploys don't race the same migration into a partial state.
    /// </summary>
    public async Task RunPendingMigrationsAsync()
    {
        var lockId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
        var lockAcquired = false;

        try
        {
            await EnsureMigrationCollectionAsync();

            lockAcquired = await TryAcquireMigrationLockAsync(lockId);
            if (!lockAcquired)
            {
                throw new InvalidOperationException(
                    $"Failed to acquire migration lock within {LockAcquireTimeout}. " +
                    "Another instance may be stuck; investigate the _migration_lock collection.");
            }

            var migrations = DiscoverMigrations();

            if (migrations.Count == 0)
            {
                _logger.LogInformation("No migrations found");
                return;
            }

            var appliedVersions = await GetAppliedMigrationsAsync();

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
        finally
        {
            if (lockAcquired)
            {
                await ReleaseMigrationLockAsync(lockId);
            }
        }
    }

    private async Task<bool> TryAcquireMigrationLockAsync(string lockId)
    {
        var locks = _database.GetCollection<MigrationLock>(LockCollectionName);

        // Ensure unique index on the lock-doc identifier so a second
        // upsert can't insert a duplicate row.
        try
        {
            await locks.Indexes.CreateOneAsync(new CreateIndexModel<MigrationLock>(
                Builders<MigrationLock>.IndexKeys.Ascending(x => x.Name),
                new CreateIndexOptions { Unique = true, Name = "ux_migration_lock_name" }));
        }
        catch (MongoCommandException ex) when (ex.Code == 86)
        {
            // IndexKeySpecsConflict — pre-existing index with different opts. Safe to ignore.
        }

        var deadline = DateTime.UtcNow + LockAcquireTimeout;

        while (DateTime.UtcNow < deadline)
        {
            var now = DateTime.UtcNow;
            var staleCutoff = now - LockTtl;

            // Atomic compare-and-set: take the lock only if it's missing or stale.
            var filter = Builders<MigrationLock>.Filter.And(
                Builders<MigrationLock>.Filter.Eq(x => x.Name, "global"),
                Builders<MigrationLock>.Filter.Or(
                    Builders<MigrationLock>.Filter.Eq(x => x.Owner, null),
                    Builders<MigrationLock>.Filter.Lt(x => x.AcquiredAt, staleCutoff)));

            var update = Builders<MigrationLock>.Update
                .Set(x => x.Owner, lockId)
                .Set(x => x.AcquiredAt, now)
                .SetOnInsert(x => x.Name, "global");

            try
            {
                var result = await locks.FindOneAndUpdateAsync(
                    filter,
                    update,
                    new FindOneAndUpdateOptions<MigrationLock>
                    {
                        IsUpsert = true,
                        ReturnDocument = ReturnDocument.After
                    });

                if (result?.Owner == lockId)
                {
                    _logger.LogInformation("Acquired migration lock (owner={Owner})", lockId);
                    return true;
                }
            }
            catch (MongoCommandException ex) when (ex.Code == 11000)
            {
                // DuplicateKey — another instance holds the lock. Fall through to retry.
            }

            _logger.LogInformation("Migration lock held by another instance; waiting...");
            await Task.Delay(LockPollInterval);
        }

        return false;
    }

    private async Task ReleaseMigrationLockAsync(string lockId)
    {
        try
        {
            var locks = _database.GetCollection<MigrationLock>(LockCollectionName);
            var filter = Builders<MigrationLock>.Filter.And(
                Builders<MigrationLock>.Filter.Eq(x => x.Name, "global"),
                Builders<MigrationLock>.Filter.Eq(x => x.Owner, lockId));

            var update = Builders<MigrationLock>.Update
                .Set(x => x.Owner, (string?)null)
                .Set(x => x.AcquiredAt, DateTime.MinValue);

            await locks.UpdateOneAsync(filter, update);
            _logger.LogInformation("Released migration lock (owner={Owner})", lockId);
        }
        catch (Exception ex)
        {
            // Lock will be reclaimed via the stale-cutoff path on the next run.
            _logger.LogWarning(ex, "Failed to release migration lock; will rely on TTL recovery");
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
