using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System;
using System.Collections.Generic;

namespace FixIt.Models
{
    /// <summary>
    /// Audit log entry tracking sensitive admin actions for compliance and security audits.
    /// </summary>
    [BsonIgnoreExtraElements]
    public class AuditLog
    {
        [BsonId]
        public ObjectId Id { get; set; }

        /// <summary>
        /// Event type (e.g., "UserRoleChanged", "AccountDisabled", "IssueDeleted", "AdminLogin")
        /// </summary>
        [BsonElement("eventType")]
        public string EventType { get; set; } = null!;

        /// <summary>
        /// Action performed (e.g., "Create", "Update", "Delete", "Login")
        /// </summary>
        [BsonElement("action")]
        public string Action { get; set; } = null!;

        /// <summary>
        /// Resource type affected (e.g., "User", "Issue", "Comment", "Report")
        /// </summary>
        [BsonElement("resource")]
        public string Resource { get; set; } = null!;

        /// <summary>
        /// ID of the affected resource
        /// </summary>
        [BsonElement("resourceId")]
        public string ResourceId { get; set; } = null!;

        /// <summary>
        /// User ID of the admin who performed the action
        /// </summary>
        [BsonElement("actorId")]
        public string ActorId { get; set; } = null!;

        /// <summary>
        /// Email/name of the admin who performed the action
        /// </summary>
        [BsonElement("actorName")]
        public string ActorName { get; set; } = null!;

        /// <summary>
        /// Role of the actor (e.g., "Admin", "Moderator")
        /// </summary>
        [BsonElement("actorRole")]
        public string ActorRole { get; set; } = null!;

        /// <summary>
        /// UTC timestamp when the action occurred
        /// </summary>
        [BsonElement("timestamp")]
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Source IP address
        /// </summary>
        [BsonElement("ipAddress")]
        public string IpAddress { get; set; } = null!;

        /// <summary>
        /// User agent/browser information
        /// </summary>
        [BsonElement("userAgent")]
        public string UserAgent { get; set; } = null!;

        /// <summary>
        /// Dictionary of changes made (keys and values of what changed)
        /// </summary>
        [BsonElement("changes")]
        public Dictionary<string, object> Changes { get; set; } = new Dictionary<string, object>();

        /// <summary>
        /// Optional reason/justification for the action
        /// </summary>
        [BsonElement("reason")]
        public string? Reason { get; set; }

        /// <summary>
        /// Status: "Success" or "Failed"
        /// </summary>
        [BsonElement("status")]
        public string Status { get; set; } = "Success";

        /// <summary>
        /// If Status is "Failed", the error message
        /// </summary>
        [BsonElement("errorMessage")]
        public string? ErrorMessage { get; set; }
    }
}
