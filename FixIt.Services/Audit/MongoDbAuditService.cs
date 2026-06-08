using FixIt.Models;
using FixIt.Models.Infrastructure;
using FixIt.Services.Contracts;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Claims;

namespace FixIt.Services
{
    /// <summary>
    /// MongoDB implementation of audit logging service.
    /// Tracks admin actions for compliance, security auditing, and accountability.
    /// </summary>
    public class MongoDbAuditService : IAuditService
    {
        private readonly IMongoCollection<AuditLog> _auditLogsCollection;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<MongoDbAuditService> _logger;

        public MongoDbAuditService(
            IMongoClient mongoClient,
            IHttpContextAccessor httpContextAccessor,
            IOptions<MongoDbSettings> mongoSettingsOptions,
            ILogger<MongoDbAuditService> logger
        )
        {
            var mongoSettings = mongoSettingsOptions.Value;
            if (string.IsNullOrWhiteSpace(mongoSettings.DatabaseName))
            {
                throw new InvalidOperationException("MongoDB database name is not configured for audit logging.");
            }

            var database = mongoClient.GetDatabase(mongoSettings.DatabaseName);
            _auditLogsCollection = database.GetCollection<AuditLog>("audit-logs");
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            
            // Ensure indices exist
            EnsureIndices();
        }

        /// <summary>
        /// Create database indices for efficient querying.
        /// </summary>
        private void EnsureIndices()
        {
            try
            {
                // Index by timestamp (descending - most recent first)
                _auditLogsCollection.Indexes.CreateOne(
                    new CreateIndexModel<AuditLog>(
                        Builders<AuditLog>.IndexKeys.Descending(x => x.Timestamp),
                        new CreateIndexOptions { Name = "timestamp_index" }
                    )
                );

                // Index by actor (who did it)
                _auditLogsCollection.Indexes.CreateOne(
                    new CreateIndexModel<AuditLog>(
                        Builders<AuditLog>.IndexKeys
                            .Ascending(x => x.ActorId)
                            .Descending(x => x.Timestamp),
                        new CreateIndexOptions { Name = "actor_timestamp_index" }
                    )
                );

                // Index by resource (what was affected)
                _auditLogsCollection.Indexes.CreateOne(
                    new CreateIndexModel<AuditLog>(
                        Builders<AuditLog>.IndexKeys
                            .Ascending(x => x.Resource)
                            .Ascending(x => x.ResourceId),
                        new CreateIndexOptions { Name = "resource_index" }
                    )
                );

                // Index by event type
                _auditLogsCollection.Indexes.CreateOne(
                    new CreateIndexModel<AuditLog>(
                        Builders<AuditLog>.IndexKeys
                            .Ascending(x => x.EventType)
                            .Descending(x => x.Timestamp),
                        new CreateIndexOptions { Name = "eventtype_index" }
                    )
                );

                // TTL index - automatically delete logs older than 3 years
                _auditLogsCollection.Indexes.CreateOne(
                    new CreateIndexModel<AuditLog>(
                        Builders<AuditLog>.IndexKeys.Ascending(x => x.Timestamp),
                        new CreateIndexOptions 
                        { 
                            ExpireAfter = TimeSpan.FromDays(365 * 3),
                            Name = "ttl_3year_index"
                        }
                    )
                );
            }
            catch
            {
                // Indices may already exist, continue silently
            }
        }

        /// <summary>
        /// Log an event asynchronously with request context (IP, user agent).
        /// </summary>
        public async Task LogEventAsync(
            string eventType,
            string action,
            string resource,
            string resourceId,
            Dictionary<string, object> changes,
            string? reason = null,
            string status = "Success",
            string? errorMessage = null
        )
        {
            try
            {
                var httpContext = _httpContextAccessor.HttpContext;
                var actorId = httpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? httpContext?.User?.FindFirstValue("sub")
                    ?? "system";
                var actorName = httpContext?.User?.FindFirstValue(ClaimTypes.Name)
                    ?? httpContext?.User?.FindFirstValue(ClaimTypes.Email)
                    ?? "System";
                var actorRole = httpContext?.User?.FindFirstValue(ClaimTypes.Role)
                    ?? httpContext?.User?.FindFirstValue("role")
                    ?? "System";
                var ipAddress = GetClientIpAddress();
                var userAgent = httpContext?.Request?.Headers["User-Agent"].ToString() ?? "Unknown";

                // Sanitize sensitive data before logging
                var sanitizedChanges = SanitizeSensitiveData(changes);

                var auditLog = new AuditLog
                {
                    EventType = eventType,
                    Action = action,
                    Resource = resource,
                    ResourceId = resourceId,
                    ActorId = actorId,
                    ActorName = actorName,
                    ActorRole = actorRole,
                    Timestamp = DateTime.UtcNow,
                    IpAddress = ipAddress,
                    UserAgent = userAgent,
                    Changes = sanitizedChanges,
                    Reason = reason,
                    Status = status,
                    ErrorMessage = errorMessage
                };

                await _auditLogsCollection.InsertOneAsync(auditLog);
            }
            catch (Exception ex)
            {
                // Audit failure must surface in observability but must not break the
                // request — callers depend on the action they were auditing.
                _logger.LogError(ex, "Audit logging failed for event {EventType} on {Resource}", eventType, resource);
            }
        }

        /// <summary>
        /// Get audit logs with optional filtering.
        /// </summary>
        public async Task<List<AuditLog>> GetLogsAsync(
            string? eventType = null,
            string? resourceType = null,
            string? resourceId = null,
            string? actorId = null,
            DateTime? from = null,
            DateTime? to = null,
            int pageSize = 100
        )
        {
            var filterBuilder = Builders<AuditLog>.Filter;
            var filters = new List<FilterDefinition<AuditLog>>();

            if (!string.IsNullOrEmpty(eventType))
                filters.Add(filterBuilder.Eq(x => x.EventType, eventType));

            if (!string.IsNullOrEmpty(resourceType))
                filters.Add(filterBuilder.Eq(x => x.Resource, resourceType));

            if (!string.IsNullOrEmpty(resourceId))
                filters.Add(filterBuilder.Eq(x => x.ResourceId, resourceId));

            if (!string.IsNullOrEmpty(actorId))
                filters.Add(filterBuilder.Eq(x => x.ActorId, actorId));

            if (from.HasValue)
                filters.Add(filterBuilder.Gte(x => x.Timestamp, from.Value));

            if (to.HasValue)
                filters.Add(filterBuilder.Lte(x => x.Timestamp, to.Value));

            var combinedFilter = filters.Count > 0
                ? filterBuilder.And(filters)
                : filterBuilder.Empty;

            var logs = await _auditLogsCollection
                .Find(combinedFilter)
                .Sort(Builders<AuditLog>.Sort.Descending(x => x.Timestamp))
                .Limit(pageSize)
                .ToListAsync();

            return logs;
        }

        /// <summary>
        /// Get a single audit log by ID.
        /// </summary>
        public async Task<AuditLog?> GetLogAsync(string id)
        {
            if (!ObjectId.TryParse(id, out var objectId))
                return null;

            return await _auditLogsCollection
                .Find(x => x.Id == objectId)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Get count of audit logs matching criteria.
        /// </summary>
        public async Task<long> GetCountAsync(
            string? eventType = null,
            DateTime? from = null,
            DateTime? to = null
        )
        {
            var filterBuilder = Builders<AuditLog>.Filter;
            var filters = new List<FilterDefinition<AuditLog>>();

            if (!string.IsNullOrEmpty(eventType))
                filters.Add(filterBuilder.Eq(x => x.EventType, eventType));

            if (from.HasValue)
                filters.Add(filterBuilder.Gte(x => x.Timestamp, from.Value));

            if (to.HasValue)
                filters.Add(filterBuilder.Lte(x => x.Timestamp, to.Value));

            var combinedFilter = filters.Count > 0
                ? filterBuilder.And(filters)
                : filterBuilder.Empty;

            return await _auditLogsCollection.CountDocumentsAsync(combinedFilter);
        }

        /// <summary>
        /// Get the client IP address from the current request.
        /// Handles X-Forwarded-For headers for proxied requests.
        /// </summary>
        private string GetClientIpAddress()
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
                return "unknown";

            // Check for X-Forwarded-For (behind proxy/load balancer)
            if (httpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwardedFor))
                return forwardedFor.ToString().Split(',')[0].Trim();

            // Check for X-Real-IP (Nginx)
            if (httpContext.Request.Headers.TryGetValue("X-Real-IP", out var realIp))
                return realIp.ToString();

            // Fall back to remote IP
            return httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        }

        /// <summary>
        /// Sanitize sensitive data before storing in logs.
        /// Redacts passwords, tokens, and API keys.
        /// </summary>
        private Dictionary<string, object> SanitizeSensitiveData(Dictionary<string, object> changes)
        {
            if (changes == null)
                return new Dictionary<string, object>();

            var sanitized = new Dictionary<string, object>();
            var sensitiveKeywords = new[] 
            { 
                "password", "token", "secret", "key", "credential", 
                "apikey", "authorization", "bearer", "apitoken"
            };

            foreach (var kvp in changes)
            {
                if (sensitiveKeywords.Any(keyword => 
                    kvp.Key.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                {
                    sanitized[kvp.Key] = "[REDACTED]";
                }
                else
                {
                    sanitized[kvp.Key] = kvp.Value;
                }
            }

            return sanitized;
        }
    }
}
