using FixIt.Models;
using FixIt.Services.Contracts;
using FixIt.Services.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FixIt.Controllers
{
    /// <summary>
    /// Admin-only API for viewing audit logs and compliance records.
    /// All endpoints require Admin role.
    /// </summary>
    [Authorize(Policy = PolicyNames.AdminOnly)]
    [Route("api/admin/audit-logs")]
    [ApiController]
    [Produces("application/json")]
    public class AuditLogsController : ControllerBase
    {
        private readonly IAuditService _auditService;

        public AuditLogsController(IAuditService auditService)
        {
            _auditService = auditService;
        }

        /// <summary>
        /// Get audit logs with optional filtering.
        /// </summary>
        /// <param name="eventType">Filter by event type (e.g., "UserRoleChanged", "AdminLogin")</param>
        /// <param name="resourceType">Filter by resource type (e.g., "User", "Issue")</param>
        /// <param name="resourceId">Filter by specific resource ID</param>
        /// <param name="actorId">Filter by admin who performed the action</param>
        /// <param name="from">Start date (UTC)</param>
        /// <param name="to">End date (UTC)</param>
        /// <param name="pageSize">Number of records (default 50, max 500)</param>
        /// <returns>List of audit log entries</returns>
        [HttpGet]
        public async Task<ActionResult<List<AuditLog>>> GetLogs(
            [FromQuery] string? eventType = null,
            [FromQuery] string? resourceType = null,
            [FromQuery] string? resourceId = null,
            [FromQuery] string? actorId = null,
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] int pageSize = 50
        )
        {
            // Ensure reasonable page size
            if (pageSize < 1) pageSize = 50;
            if (pageSize > 500) pageSize = 500;

            // Default to last 3 months if no date range specified
            var fromDate = from ?? DateTime.UtcNow.AddMonths(-3);
            var toDate = to ?? DateTime.UtcNow;

            var logs = await _auditService.GetLogsAsync(
                eventType: eventType ?? string.Empty,
                resourceType: resourceType ?? string.Empty,
                resourceId: resourceId ?? string.Empty,
                actorId: actorId ?? string.Empty,
                from: fromDate,
                to: toDate,
                pageSize: pageSize
            );

            return Ok(logs);
        }

        /// <summary>
        /// Get a specific audit log entry by ID.
        /// </summary>
        /// <param name="id">Audit log ID (MongoDB ObjectId)</param>
        /// <returns>Single audit log entry</returns>
        [HttpGet("{id}")]
        public async Task<ActionResult<AuditLog>> GetLog(string id)
        {
            var log = await _auditService.GetLogAsync(id);
            if (log == null)
                return NotFound(new { error = "Audit log not found" });

            return Ok(log);
        }

        /// <summary>
        /// Get audit statistics for a given date range.
        /// </summary>
        /// <param name="from">Start date (UTC)</param>
        /// <param name="to">End date (UTC)</param>
        /// <returns>Summary statistics</returns>
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null
        )
        {
            var fromDate = from ?? DateTime.UtcNow.AddMonths(-1);
            var toDate = to ?? DateTime.UtcNow;

            var adminLogins = await _auditService.GetCountAsync("AdminLogin", fromDate, toDate);
            var userRoleChanges = await _auditService.GetCountAsync("UserRoleChanged", fromDate, toDate);
            var accountDisabled = await _auditService.GetCountAsync("AccountDisabled", fromDate, toDate);
            var deletions = await _auditService.GetCountAsync("IssueDeleted", fromDate, toDate);
            var total = await _auditService.GetCountAsync(string.Empty, fromDate, toDate);

            return Ok(new
            {
                period = new { from = fromDate, to = toDate },
                summary = new
                {
                    totalEvents = total,
                    adminLogins = adminLogins,
                    userRoleChanges = userRoleChanges,
                    accountDisabled = accountDisabled,
                    deletions = deletions
                }
            });
        }

        /// <summary>
        /// Export audit logs as CSV (useful for compliance reports).
        /// </summary>
        /// <param name="from">Start date (UTC)</param>
        /// <param name="to">End date (UTC)</param>
        /// <returns>CSV file download</returns>
        [HttpGet("export/csv")]
        public async Task<IActionResult> ExportCsv(
            [FromQuery] DateTime? from = null,
            [FromQuery] DateTime? to = null,
            [FromQuery] bool includeSensitive = false
        )
        {
            var fromDate = from ?? DateTime.UtcNow.AddMonths(-3);
            var toDate = to ?? DateTime.UtcNow;

            var logs = await _auditService.GetLogsAsync(
                from: fromDate,
                to: toDate,
                pageSize: 10000  // Allow large export
            );

            // Build CSV
            var csv = "Timestamp,Event Type,Action,Resource,Actor ID,Actor Name,Status,IP Address,Reason\n";
            foreach (var log in logs)
            {
                var timestamp = log.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                var actorName = includeSensitive ? log.ActorName : MaskActorName(log.ActorName);
                var ipAddress = includeSensitive ? log.IpAddress : MaskIpAddress(log.IpAddress);
                var reason = log.Reason ?? "";
                csv += string.Join(",", new[]
                {
                    EscapeCsv(timestamp),
                    EscapeCsv(log.EventType),
                    EscapeCsv(log.Action),
                    EscapeCsv(log.Resource),
                    EscapeCsv(log.ActorId),
                    EscapeCsv(actorName),
                    EscapeCsv(log.Status),
                    EscapeCsv(ipAddress),
                    EscapeCsv(reason)
                }) + "\n";
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
            return File(
                bytes,
                "text/csv",
                $"audit-logs-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv"
            );
        }

        private static string EscapeCsv(string? value)
        {
            var safeValue = value ?? string.Empty;

            // Prevent CSV formula injection when opened in spreadsheet tools.
            if (safeValue.StartsWith('=') || safeValue.StartsWith('+') || safeValue.StartsWith('-') || safeValue.StartsWith('@'))
            {
                safeValue = "'" + safeValue;
            }

            safeValue = safeValue.Replace("\"", "\"\"");
            return $"\"{safeValue}\"";
        }

        private static string MaskActorName(string? actorName)
        {
            if (string.IsNullOrWhiteSpace(actorName))
            {
                return "Unknown";
            }

            return "Redacted";
        }

        private static string MaskIpAddress(string? ipAddress)
        {
            if (string.IsNullOrWhiteSpace(ipAddress))
            {
                return "Unknown";
            }

            if (ipAddress.Contains(':'))
            {
                var segments = ipAddress.Split(':', StringSplitOptions.RemoveEmptyEntries);
                if (segments.Length >= 2)
                {
                    return $"{segments[0]}:{segments[1]}::";
                }

                return "Redacted";
            }

            var octets = ipAddress.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (octets.Length == 4)
            {
                return $"{octets[0]}.{octets[1]}.x.x";
            }

            return "Redacted";
        }
    }
}
