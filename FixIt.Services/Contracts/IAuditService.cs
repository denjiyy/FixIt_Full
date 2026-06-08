using FixIt.Models;

namespace FixIt.Services.Contracts
{
    /// <summary>
    /// Service for logging audit events to track admin actions for compliance.
    /// </summary>
    public interface IAuditService
    {
        /// <summary>
        /// Log an event asynchronously.
        /// </summary>
        Task LogEventAsync(
            string eventType,
            string action,
            string resource,
            string resourceId,
            Dictionary<string, object> changes,
            string? reason = null,
            string status = "Success",
            string? errorMessage = null
        );

        /// <summary>
        /// Get audit logs with optional filtering.
        /// </summary>
        Task<List<AuditLog>> GetLogsAsync(
            string? eventType = null,
            string? resourceType = null,
            string? resourceId = null,
            string? actorId = null,
            DateTime? from = null,
            DateTime? to = null,
            int pageSize = 100
        );

        /// <summary>
        /// Get a single audit log by ID.
        /// </summary>
        Task<AuditLog?> GetLogAsync(string id);

        /// <summary>
        /// Get count of audit logs matching the criteria.
        /// </summary>
        Task<long> GetCountAsync(
            string? eventType = null,
            DateTime? from = null,
            DateTime? to = null
        );
    }
}
