# Database Migrations Documentation

## Overview

FixIt uses a version-controlled migration system for MongoDB schema management. This ensures database changes are:

- **Tracked** - Every schema change is recorded with version and timestamp
- **Repeatable** - Migrations run in order and are idempotent (safe to run multiple times)
- **Reversible** - Rollback support for critical issues (when implemented)
- **Auditable** - Full history of schema changes in `_migrations` collection

---

## How It Works

### Migration Lifecycle

1. **Discovery** - Application startup finds all classes implementing `IMigration`
2. **Filtering** - Compares discovered migrations with applied migrations (from `_migrations` collection)
3. **Execution** - Runs pending migrations in version order
4. **Recording** - Stores success/failure in `_migrations` for audit trail

### Key Features

- **Idempotent** - Safe to restart if interrupted (only missing migrations run)
- **Ordered** - Version format ensures correct execution order
- **Tracked** - Every migration recorded with timestamp and result
- **Non-blocking** - Application fails fast if migration fails (prevents partial deployments)

---

## Creating a Migration

### 1. Create Migration File

**Naming Convention:** `Migration_YYYYMMDD_###_DescriptionInPascalCase.cs`

- `YYYYMMDD` - Date migration was created
- `###` - Sequential number (001, 002, 003...)
- Description should be clear and descriptive

**Example:**
```csharp
// File: Migration_20240104_001_AddUserPreferences.cs

using MongoDB.Driver;

namespace FixIt.Data.Infrastructure.Migrations;

public class Migration_20240104_001_AddUserPreferences : IMigration
{
    public string Version => "20240104_001";
    public string Description => "Add user preferences collection and indexes";

    public async Task UpAsync(IMongoDatabase database)
    {
        var collection = database.GetCollection<BsonDocument>("userPreferences");
        
        // Create index for user lookup
        var indexModel = new CreateIndexModel<BsonDocument>(
            Builders<BsonDocument>.IndexKeys.Ascending("userId"),
            new CreateIndexOptions { Unique = true }
        );
        
        try
        {
            await collection.Indexes.CreateOneAsync(indexModel);
        }
        catch (MongoCommandException ex) when (ex.Code == 68) // Index already exists
        {
            // Idempotent: Index already created
        }
    }
}
```

### 2. Key Principles

**✅ DO:**
- Handle `MongoCommandException` with code 68 (index already exists)
- Use descriptive version numbers
- Include comments explaining the change
- Test migration locally first
- Keep migrations focused on one change

**❌ DON'T:**
- Use timestamps as version numbers (not sortable)
- Skip error handling for existing resources
- Make assumptions about existing data
- Run data transformations in production without testing

---

## Migration Types

### 1. **Index Creation**
```csharp
// Create performance indexes
var indexModel = new CreateIndexModel<BsonDocument>(
    Builders<BsonDocument>.IndexKeys
        .Ascending("status")
        .Descending("createdDate"),
    new CreateIndexOptions { Name = "idx_status_created" }
);
```

### 2. **Schema Changes**
```csharp
// Add field to existing documents
var filter = Builders<BsonDocument>.Filter.Exists("newField", false);
var update = Builders<BsonDocument>.Update.Set("newField", "defaultValue");
await collection.UpdateManyAsync(filter, update);
```

### 3. **Collection Creation**
```csharp
// Create new collection with indexes
await database.CreateCollectionAsync("newCollection");
var collection = database.GetCollection<BsonDocument>("newCollection");
// Add indexes...
```

### 4. **TTL Indexes**
```csharp
// Auto-delete documents after expiration
await collection.Indexes.CreateOneAsync(
    new CreateIndexModel<BsonDocument>(
        Builders<BsonDocument>.IndexKeys.Ascending("createdAt"),
        new CreateIndexOptions 
        { 
            ExpireAfter = TimeSpan.FromDays(30) 
        }
    )
);
```

---

## Running Migrations

### Automatic (Recommended)

Migrations run automatically on application startup via `Program.cs`:

```csharp
// Run pending database migrations on startup
using (var scope = app.Services.CreateScope())
{
    var migrationRunner = scope.ServiceProvider.GetRequiredService<MigrationRunner>();
    await migrationRunner.RunPendingMigrationsAsync();
}
```

**When to use:**
- Normal deployments
- CI/CD pipelines
- Development environments

### Manual (Emergency/Testing)

```csharp
// In a test or admin tool
var client = new MongoClient(connectionString);
var database = client.GetDatabase("fixit");
var logger = LoggerFactory.Create(c => c.AddConsole())
    .CreateLogger<MigrationRunner>();

var runner = new MigrationRunner(database, logger);
await runner.RunPendingMigrationsAsync();
```

---

## Checking Migration Status

### Query Migration History

```csharp
var runner = scope.ServiceProvider.GetRequiredService<MigrationRunner>();

// Get migration history since a date
var history = await runner.GetMigrationHistoryAsync(
    since: DateTime.UtcNow.AddDays(-7)
);

foreach (var record in history)
{
    Console.WriteLine($"{record.Version} - {record.Description}");
    Console.WriteLine($"  Applied: {record.AppliedAt:G}");
    Console.WriteLine($"  Success: {record.Success}");
    if (!record.Success)
    {
        Console.WriteLine($"  Error: {record.Error}");
    }
}
```

### Check MongoDB Directly

```bash
# Connect to MongoDB
mongosh -u root -p rootpassword

# Switch to fixit database
use fixit

# View all applied migrations
db._migrations.find()

# View failed migrations
db._migrations.find({ success: false })

# View migrations by date
db._migrations.find({ 
    appliedAt: { $gte: new Date("2024-01-01") } 
}).sort({ appliedAt: 1 })
```

---

## Development Workflow

### When Modifying Database Schema

1. **Create migration file** - Following naming convention
2. **Write idempotent `UpAsync` method** - Handle existing resources
3. **Test locally**:
   ```bash
   docker-compose down -v  # Clean slate
   docker-compose up -d
   # App runs migrations automatically
   ```
4. **Verify in MongoDB**:
   ```bash
   docker-compose exec mongodb mongosh -u root -p rootpassword
   use fixit
   db._migrations.find()
   db.issues.getIndexes()  # or relevant collection
   ```
5. **Commit to git** - Include migration file with code changes
6. **Deploy** - Migrations run automatically on startup

---

## Production Deployment

### Pre-Deployment Checklist

- [ ] Migration tested locally
- [ ] Version number is unique and future-dated if needed
- [ ] Error handling for existing resources (idempotency)
- [ ] Rollback plan if needed (implement `DownAsync`)
- [ ] Migration history reviewed
- [ ] No data loss anticipated

### Deployment Process

1. **Backup database** (automated on production)
   ```bash
   # MongoDB Atlas: Automatic snapshots
   # or manually: mongodump
   ```

2. **Deploy code** (includes migrations)
   ```bash
   docker pull ghcr.io/denjiyy/fixit:latest
   docker-compose -f docker-compose.yml -f docker-compose.prod.yml up -d
   ```

3. **Monitor startup**
   ```bash
   docker-compose logs -f fixit-app
   # Watch for: "All migrations completed successfully"
   ```

4. **Verify schema**
   ```bash
   db._migrations.find().sort({ appliedAt: -1 }).limit(5)
   ```

---

## Troubleshooting

### Migration Failed - Cannot Rollback

**Symptoms:** Application won't start, old migration failed

**Recovery:**
1. Check error in `_migrations` collection
2. Fix the migration code
3. Delete failed record: `db._migrations.deleteOne({ version: "20240104_001" })`
4. Restart application to re-run

### Migration Hangs

**Symptoms:** Application startup takes too long

**Check:**
```bash
# View MongoDB logs for slow operations
db.setProfilingLevel(1)  # Enable profiling
db.system.profile.find().sort({ ts: -1 }).limit(5)

# Check current operations
db.currentOp()
```

### Index Already Exists

**Expected behavior** - Handled in migration:
```csharp
catch (MongoCommandException ex) when (ex.Code == 68)
{
    // Index already exists - this is fine
}
```

### Data Corruption During Migration

**Prevention:**
- Always backup before production migrations
- Test with production-like dataset locally
- Use transactions for multi-step changes (MongoDB 4.0+)

**Recovery:**
- Restore from backup
- Implement `DownAsync` to revert
- Fix migration and re-deploy

---

## Best Practices

### Schema Design

1. **Add fields with defaults** - Never require new fields on existing documents
2. **Use nullable types** - Allow gradual migration of data
3. **Index before querying** - Add indexes before using new query patterns
4. **Soft deletes** - Don't delete collections, mark with `deletedAt`

### Migration Sizing

- **One migration = One change** - Easier to debug and rollback
- **Limit scope** - Don't transform entire collections in one migration
- **Batch large operations** - Process documents in chunks for huge collections

### Documentation

```csharp
/// <summary>
/// Migration to add audit timestamp indexes for compliance
/// Purpose: Improve query performance for audit log retrieval
/// Expected duration: < 1 minute on production (10M+ documents)
/// Rollback: Drop indexes manually if needed
/// </summary>
public class Migration_20240105_001_AddAuditIndexes : IMigration
{
    public string Version => "20240105_001";
    public string Description => "Add indexes to audit timestamp fields";
    
    // Implementation...
}
```

---

## Example Migrations

See the following example files in `FixIt.Data/Infrastructure/Migrations/`:

1. **Migration_20240101_001_CreateIndexes.cs** - Index creation
2. **Migration_20240102_001_AddSessionTtl.cs** - TTL index for session expiry
3. **Migration_20240103_001_AddIssueSafetyValidation.cs** - Schema enhancement

---

## Future Enhancements

Potential improvements to migration system:

- [ ] Automatic rollback on failure with retry
- [ ] Migration dry-run mode (validate without executing)
- [ ] Parallel migration execution for independent operations
- [ ] Schema validation and change detection
- [ ] Integration with MongoDB Ops Manager for enterprise deployments

---

For questions or migration examples, see [README.md](../README.md) or [DOCKER.md](../DOCKER.md).
