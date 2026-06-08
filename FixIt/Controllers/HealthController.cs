using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using MongoDB.Driver;

namespace FixIt.Controllers;

/// <summary>
/// Health check endpoints for monitoring and orchestration
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly IMongoClient _mongoClient;
    private readonly IMongoDatabase _mongoDatabase;
    private readonly ILogger<HealthController> _logger;

    public HealthController(IMongoClient mongoClient, IMongoDatabase mongoDatabase, ILogger<HealthController> logger)
    {
        _mongoClient = mongoClient;
        _mongoDatabase = mongoDatabase;
        _logger = logger;
    }

    /// <summary>
    /// Liveness probe - indicates if the service is running
    /// Used by orchestrators (Kubernetes, Docker, etc.) to determine if the pod/container should be restarted
    /// Returns 200 if the application is running
    /// </summary>
    [HttpGet("/health")]
    [HttpGet("/health/live")]
    [OutputCache(PolicyName = "health-cache")]
    public ActionResult<HealthResponse> GetLiveness()
    {
        try
        {
            // Quick check - just verify the application is responding
            var response = new HealthResponse
            {
                Status = "healthy",
                Timestamp = DateTime.UtcNow,
                Version = "1.0",
                Checks = new Dictionary<string, HealthCheck>
                {
                    { "application", new HealthCheck { Status = "healthy", Message = "Application is running" } }
                }
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Health check failed");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new HealthResponse
            {
                Status = "unhealthy",
                Timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// Readiness probe - indicates if the service is ready to accept traffic
    /// Checks all dependencies (database, external services, etc.)
    /// Used to determine if traffic should be routed to this instance
    /// Returns 200 if all dependencies are healthy
    /// </summary>
    [HttpGet("/health/ready")]
    [HttpGet("/ready")]
    [OutputCache(PolicyName = "health-cache")]
    public async Task<ActionResult<HealthResponse>> GetReadiness()
    {
        var checks = new Dictionary<string, HealthCheck>();
        var allHealthy = true;

        // Check MongoDB connectivity
        try
        {
            var adminDb = _mongoClient.GetDatabase("admin");
            var command = new MongoDB.Bson.BsonDocument("ping", 1);
            await adminDb.RunCommandAsync<MongoDB.Bson.BsonDocument>(command);
            
            checks.Add("mongodb", new HealthCheck
            {
                Status = "healthy",
                Message = "MongoDB is reachable"
            });
        }
        catch (Exception ex)
        {
            allHealthy = false;
            _logger.LogWarning(ex, "MongoDB health check failed");
            checks.Add("mongodb", new HealthCheck
            {
                Status = "unhealthy",
                Message = $"MongoDB connection failed: {ex.Message}"
            });
        }

        // Check application database
        try
        {
            var collections = await _mongoDatabase.ListCollectionNamesAsync();
            await collections.ToListAsync();
            
            checks.Add("database", new HealthCheck
            {
                Status = "healthy",
                Message = $"Database '{_mongoDatabase.DatabaseNamespace.DatabaseName}' is accessible"
            });
        }
        catch (Exception ex)
        {
            allHealthy = false;
            _logger.LogWarning(ex, "Database health check failed");
            checks.Add("database", new HealthCheck
            {
                Status = "unhealthy",
                Message = $"Database access failed: {ex.Message}"
            });
        }

        var response = new HealthResponse
        {
            Status = allHealthy ? "healthy" : "unhealthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0",
            Checks = checks
        };

        return allHealthy ? Ok(response) : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }

    /// <summary>
    /// Detailed health information including version and dependencies
    /// </summary>
    [HttpGet("/health/detailed")]
    [OutputCache(PolicyName = "health-cache")]
    public async Task<ActionResult<HealthResponse>> GetDetailed()
    {
        var checks = new Dictionary<string, HealthCheck>();
        var allHealthy = true;

        // Check MongoDB
        try
        {
            var adminDb = _mongoClient.GetDatabase("admin");
            var command = new MongoDB.Bson.BsonDocument("ping", 1);
            await adminDb.RunCommandAsync<MongoDB.Bson.BsonDocument>(command);
            
            checks.Add("mongodb", new HealthCheck
            {
                Status = "healthy",
                Message = "MongoDB is reachable",
                Details = new { version = "3.6+" }
            });
        }
        catch (Exception ex)
        {
            allHealthy = false;
            _logger.LogWarning(ex, "MongoDB health check failed");
            checks.Add("mongodb", new HealthCheck
            {
                Status = "unhealthy",
                Message = $"MongoDB failed: {ex.Message}",
                Details = new { error = ex.GetType().Name }
            });
        }

        // Check application runtime
        checks.Add("runtime", new HealthCheck
        {
            Status = "healthy",
            Message = ".NET 9.0",
            Details = new
            {
                framework = ".NET 9.0",
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
                uptime = GC.GetTotalMemory(false) / 1024 / 1024 + " MB"
            }
        });

        var response = new HealthResponse
        {
            Status = allHealthy ? "healthy" : "unhealthy",
            Timestamp = DateTime.UtcNow,
            Version = "1.0",
            Checks = checks
        };

        return allHealthy ? Ok(response) : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }
}

/// <summary>
/// Response model for health checks
/// </summary>
public class HealthResponse
{
    /// <summary>
    /// Overall status: "healthy" or "unhealthy"
    /// </summary>
    public string Status { get; set; } = "unknown";

    /// <summary>
    /// When this check was performed
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// API version
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Individual component health checks
    /// </summary>
    public Dictionary<string, HealthCheck> Checks { get; set; } = new();
}

/// <summary>
/// Individual health check result
/// </summary>
public class HealthCheck
{
    /// <summary>
    /// Status of this component: "healthy", "degraded", or "unhealthy"
    /// </summary>
    public string Status { get; set; } = "unknown";

    /// <summary>
    /// Human-readable description
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Additional metadata about this check
    /// </summary>
    public object? Details { get; set; }
}
