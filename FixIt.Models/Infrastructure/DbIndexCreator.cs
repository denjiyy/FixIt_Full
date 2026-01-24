using MongoDB.Bson;
using MongoDB.Driver;
using FixIt.Models.Users;
using FixIt.Models.Issues;
using FixIt.Models.Engagement;
using FixIt.Models.Notifications;
using FixIt.Models.Moderation;
using FixIt.Models.Media;

namespace FixIt.Models.Infrastructure;

public static class DbIndexCreator
{
    public static void EnsureIndexesAndValidators(IMongoDatabase db)
    {
        CreateIdentityIndexes(db);
        CreateUserReputationIndexes(db);
        CreateRateLimitIndexes(db);
        CreateIssueIndexes(db);
        CreateTagIndexes(db);
        CreateOfficialResponseIndexes(db);
        CreateEngagementIndexes(db);
        CreateMediaIndexes(db);
        CreateMediaReferenceIndexes(db);
        CreateNotificationIndexes(db);
        CreateSubscriptionIndexes(db);
        CreateModerationIndexes(db);
        EnsureIssueValidator(db);
    }

    #region Identity and User Management

    static void CreateIdentityIndexes(IMongoDatabase db)
    {
        var users = db.GetCollection<ApplicationUser>("AspNetUsers");

        // Required for ASP.NET Identity
        users.Indexes.CreateOne(new CreateIndexModel<ApplicationUser>(
            Builders<ApplicationUser>.IndexKeys.Ascending(u => u.NormalizedUserName),
            new CreateIndexOptions { Unique = true, Name = "ux_users_normalizedusername" }
        ));

        users.Indexes.CreateOne(new CreateIndexModel<ApplicationUser>(
            Builders<ApplicationUser>.IndexKeys.Ascending(u => u.NormalizedEmail),
            new CreateIndexOptions { Name = "ix_users_normalizedemail" }
        ));

        // For finding users by role and reputation
        // Useful when you want to find all moderators or highly-reputed users
        users.Indexes.CreateOne(new CreateIndexModel<ApplicationUser>(
            Builders<ApplicationUser>.IndexKeys
                .Ascending(u => u.Role)
                .Descending(u => u.ReputationScore),
            new CreateIndexOptions { Name = "ix_users_role_reputation" }
        ));

        // For filtering out deleted users efficiently
        users.Indexes.CreateOne(new CreateIndexModel<ApplicationUser>(
            Builders<ApplicationUser>.IndexKeys.Ascending(u => u.IsDeleted),
            new CreateIndexOptions { Name = "ix_users_isdeleted" }
        ));
    }

    static void CreateUserReputationIndexes(IMongoDatabase db)
    {
        var reputations = db.GetCollection<UserReputation>("user_reputations");

        // One reputation document per user - enforce uniqueness
        reputations.Indexes.CreateOne(new CreateIndexModel<UserReputation>(
            Builders<UserReputation>.IndexKeys.Ascending(r => r.UserId),
            new CreateIndexOptions { Unique = true, Name = "ux_reputations_userid" }
        ));

        // For finding top contributors
        reputations.Indexes.CreateOne(new CreateIndexModel<UserReputation>(
            Builders<UserReputation>.IndexKeys.Descending(r => r.ReputationScore),
            new CreateIndexOptions { Name = "ix_reputations_score" }
        ));

        // For identifying users who need moderation attention
        reputations.Indexes.CreateOne(new CreateIndexModel<UserReputation>(
            Builders<UserReputation>.IndexKeys.Descending(r => r.AbuseReportsReceived),
            new CreateIndexOptions { Name = "ix_reputations_abuse_reports" }
        ));
    }

    static void CreateRateLimitIndexes(IMongoDatabase db)
    {
        var rateLimits = db.GetCollection<RateLimitEntry>("rate_limits");

        // For checking if a user/IP has exceeded their rate limit
        // We query: "show me all CreateIssue actions by user X in the last hour"
        rateLimits.Indexes.CreateOne(new CreateIndexModel<RateLimitEntry>(
            Builders<RateLimitEntry>.IndexKeys
                .Ascending(r => r.Identifier)
                .Ascending(r => r.ActionType)
                .Descending(r => r.Timestamp),
            new CreateIndexOptions { Name = "ix_ratelimits_identifier_action_time" }
        ));

        // TTL index to automatically delete old entries
        // MongoDB will automatically remove documents where ExpiresAt < current time
        rateLimits.Indexes.CreateOne(new CreateIndexModel<RateLimitEntry>(
            Builders<RateLimitEntry>.IndexKeys.Ascending(r => r.ExpiresAt),
            new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "ttl_ratelimits_expiresat" }
        ));
    }

    #endregion

    #region Issues and Tags

    static void CreateIssueIndexes(IMongoDatabase db)
    {
        var issues = db.GetCollection<Issue>("issues");

        // Geospatial index for "find issues near me"
        issues.Indexes.CreateOne(new CreateIndexModel<Issue>(
            Builders<Issue>.IndexKeys.Geo2DSphere(i => i.Location),
            new CreateIndexOptions { Name = "idx_issues_location_2dsphere" }
        ));

        // Text search index for title, description
        // Note: removed Tags from text index since we now use TagIds
        issues.Indexes.CreateOne(new CreateIndexModel<Issue>(
            Builders<Issue>.IndexKeys
                .Text(i => i.Title)
                .Text(i => i.Description),
            new CreateIndexOptions { Name = "idx_issues_text" }
        ));

        // Compound index for common query: "show me open issues in my city, newest first"
        issues.Indexes.CreateOne(new CreateIndexModel<Issue>(
            Builders<Issue>.IndexKeys
                .Ascending(i => i.CityId)
                .Ascending(i => i.Status)
                .Descending(i => i.CreatedAt),
            new CreateIndexOptions { Name = "idx_issues_city_status_created" }
        ));

        // For finding a user's issues
        issues.Indexes.CreateOne(new CreateIndexModel<Issue>(
            Builders<Issue>.IndexKeys.Ascending("Reporter.Id"),
            new CreateIndexOptions { Name = "idx_issues_reporter_id" }
        ));

        // For "hot" or "trending" queries: sort by recent activity
        issues.Indexes.CreateOne(new CreateIndexModel<Issue>(
            Builders<Issue>.IndexKeys
                .Ascending(i => i.CityId)
                .Descending(i => i.LastActivityAt),
            new CreateIndexOptions { Name = "idx_issues_city_activity" }
        ));

        // For "popular" queries: sort by engagement (votes + comments)
        // This is a compound sort, so we index for it
        issues.Indexes.CreateOne(new CreateIndexModel<Issue>(
            Builders<Issue>.IndexKeys
                .Ascending(i => i.CityId)
                .Descending(i => i.Upvotes),
            new CreateIndexOptions { Name = "idx_issues_city_upvotes" }
        ));

        // Partial index for active issues (not deleted)
        CreatePartialRecentIssuesIndex(db);
    }

    static void CreatePartialRecentIssuesIndex(IMongoDatabase db)
    {
        // This index only includes documents where IsDeleted = false
        // This makes queries for active content much faster and smaller
        var command = new BsonDocument
        {
            { "createIndexes", "issues" },
            { "indexes", new BsonArray
                {
                    new BsonDocument
                    {
                        { "name", "idx_issues_created_recent" },
                        { "key", new BsonDocument("CreatedAt", 1) },
                        { "partialFilterExpression", new BsonDocument("IsDeleted", false) }
                    }
                }
            }
        };

        db.RunCommand<BsonDocument>(command);
    }

    static void CreateTagIndexes(IMongoDatabase db)
    {
        var tags = db.GetCollection<Tag>("tags");

        // Unique tag names prevent duplicates
        tags.Indexes.CreateOne(new CreateIndexModel<Tag>(
            Builders<Tag>.IndexKeys.Ascending(t => t.Name),
            new CreateIndexOptions { Unique = true, Name = "ux_tags_name" }
        ));

        // For autocomplete: find tags starting with "pot..."
        // Text index would work, but a simple ascending index is faster for prefix matching
        tags.Indexes.CreateOne(new CreateIndexModel<Tag>(
            Builders<Tag>.IndexKeys.Ascending(t => t.Name),
            new CreateIndexOptions { Name = "ix_tags_name_prefix" }
        ));

        // For showing popular tags
        tags.Indexes.CreateOne(new CreateIndexModel<Tag>(
            Builders<Tag>.IndexKeys.Descending(t => t.UsageCount),
            new CreateIndexOptions { Name = "ix_tags_usage" }
        ));

        // For finding tags by category
        tags.Indexes.CreateOne(new CreateIndexModel<Tag>(
            Builders<Tag>.IndexKeys.Ascending(t => t.Category),
            new CreateIndexOptions { Name = "ix_tags_category" }
        ));
    }

    static void CreateOfficialResponseIndexes(IMongoDatabase db)
    {
        var responses = db.GetCollection<OfficialResponse>("official_responses");

        // For finding all responses to an issue
        responses.Indexes.CreateOne(new CreateIndexModel<OfficialResponse>(
            Builders<OfficialResponse>.IndexKeys
                .Ascending(r => r.IssueId)
                .Descending(r => r.CreatedAt),
            new CreateIndexOptions { Name = "ix_responses_issue_created" }
        ));

        // For finding all responses by a department
        responses.Indexes.CreateOne(new CreateIndexModel<OfficialResponse>(
            Builders<OfficialResponse>.IndexKeys
                .Ascending(r => r.Department)
                .Descending(r => r.CreatedAt),
            new CreateIndexOptions { Name = "ix_responses_department_created" }
        ));

        // For finding responses by reference number
        responses.Indexes.CreateOne(new CreateIndexModel<OfficialResponse>(
            Builders<OfficialResponse>.IndexKeys.Ascending(r => r.ReferenceNumber),
            new CreateIndexOptions { Name = "ix_responses_reference" }
        ));
    }

    #endregion

    #region Engagement

    static void CreateEngagementIndexes(IMongoDatabase db)
    {
        var votes = db.GetCollection<Vote>("votes");

        // One vote per user per issue
        votes.Indexes.CreateOne(new CreateIndexModel<Vote>(
            Builders<Vote>.IndexKeys
                .Ascending(v => v.IssueId)
                .Ascending(v => v.UserId),
            new CreateIndexOptions { Unique = true, Name = "ux_votes_issue_user" }
        ));

        // For finding all votes by a user
        votes.Indexes.CreateOne(new CreateIndexModel<Vote>(
            Builders<Vote>.IndexKeys.Ascending(v => v.UserId),
            new CreateIndexOptions { Name = "ix_votes_user" }
        ));

        var comments = db.GetCollection<Comment>("comments");

        // For showing comments on an issue, newest first
        comments.Indexes.CreateOne(new CreateIndexModel<Comment>(
            Builders<Comment>.IndexKeys
                .Ascending(c => c.IssueId)
                .Descending(c => c.CreatedAt),
            new CreateIndexOptions { Name = "idx_comments_issue_created" }
        ));

        // For finding all comments by a user
        comments.Indexes.CreateOne(new CreateIndexModel<Comment>(
            Builders<Comment>.IndexKeys.Ascending(c => c.AuthorId),
            new CreateIndexOptions { Name = "idx_comments_author" }
        ));

        // For filtering out deleted comments
        comments.Indexes.CreateOne(new CreateIndexModel<Comment>(
            Builders<Comment>.IndexKeys.Ascending(c => c.IsDeleted),
            new CreateIndexOptions { Name = "ix_comments_isdeleted" }
        ));
    }

    #endregion

    #region Media

    static void CreateMediaIndexes(IMongoDatabase db)
    {
        var media = db.GetCollection<FixIt.Models.Media.Media>("media");

        // For finding all media owned by a user
        media.Indexes.CreateOne(new CreateIndexModel<FixIt.Models.Media.Media>(
            Builders<FixIt.Models.Media.Media>.IndexKeys
                .Ascending(m => m.OwnerId)
                .Descending(m => m.CreatedAt),
            new CreateIndexOptions { Name = "idx_media_owner_created" }
        ));

        // For finding large files that might need cleanup
        media.Indexes.CreateOne(new CreateIndexModel<FixIt.Models.Media.Media>(
            Builders<FixIt.Models.Media.Media>.IndexKeys.Descending(m => m.SizeBytes),
            new CreateIndexOptions { Name = "ix_media_size" }
        ));
    }

    static void CreateMediaReferenceIndexes(IMongoDatabase db)
    {
        var refs = db.GetCollection<MediaReference>("media_references");

        // For finding all places a media file is used
        refs.Indexes.CreateOne(new CreateIndexModel<MediaReference>(
            Builders<MediaReference>.IndexKeys.Ascending(r => r.MediaId),
            new CreateIndexOptions { Name = "ix_media_refs_mediaid" }
        ));

        // For finding all media attached to an entity
        // (e.g., "show me all media on this issue")
        refs.Indexes.CreateOne(new CreateIndexModel<MediaReference>(
            Builders<MediaReference>.IndexKeys
                .Ascending(r => r.ReferenceType)
                .Ascending(r => r.ReferenceId),
            new CreateIndexOptions { Name = "ix_media_refs_reference" }
        ));

        // Compound index for the common "find orphaned media" query
        refs.Indexes.CreateOne(new CreateIndexModel<MediaReference>(
            Builders<MediaReference>.IndexKeys
                .Ascending(r => r.MediaId)
                .Ascending(r => r.ReferenceType),
            new CreateIndexOptions { Name = "ix_media_refs_media_type" }
        ));
    }

    #endregion

    #region Notifications

    static void CreateNotificationIndexes(IMongoDatabase db)
    {
        var notifications = db.GetCollection<Notification>("notifications");

        // For showing a user's notifications, unread first, then newest
        notifications.Indexes.CreateOne(new CreateIndexModel<Notification>(
            Builders<Notification>.IndexKeys
                .Ascending(n => n.UserId)
                .Ascending(n => n.IsRead)
                .Descending(n => n.CreatedAt),
            new CreateIndexOptions { Name = "ix_notifications_user_read_created" }
        ));

        // For finding notifications related to an issue
        notifications.Indexes.CreateOne(new CreateIndexModel<Notification>(
            Builders<Notification>.IndexKeys.Ascending(n => n.RelatedIssueId),
            new CreateIndexOptions { Name = "ix_notifications_issue" }
        ));

        // TTL index to auto-delete expired notifications
        notifications.Indexes.CreateOne(new CreateIndexModel<Notification>(
            Builders<Notification>.IndexKeys.Ascending(n => n.ExpiresAt),
            new CreateIndexOptions { ExpireAfter = TimeSpan.Zero, Name = "ttl_notifications_expiresat" }
        ));
    }

    static void CreateSubscriptionIndexes(IMongoDatabase db)
    {
        var subs = db.GetCollection<NotificationSubscription>("subscriptions");

        // Geospatial index for finding subscriptions near a location
        // When a new issue is created, we query: "which subscriptions are within radius?"
        subs.Indexes.CreateOne(new CreateIndexModel<NotificationSubscription>(
            Builders<NotificationSubscription>.IndexKeys.Geo2DSphere(s => s.Center),
            new CreateIndexOptions { Name = "idx_subscriptions_center_2dsphere" }
        ));

        // For finding all subscriptions for a user
        subs.Indexes.CreateOne(new CreateIndexModel<NotificationSubscription>(
            Builders<NotificationSubscription>.IndexKeys.Ascending(s => s.UserId),
            new CreateIndexOptions { Name = "ix_subscriptions_user" }
        ));

        // For finding all subscriptions in a city
        subs.Indexes.CreateOne(new CreateIndexModel<NotificationSubscription>(
            Builders<NotificationSubscription>.IndexKeys.Ascending(s => s.CityId),
            new CreateIndexOptions { Name = "ix_subscriptions_city" }
        ));
    }

    #endregion

    #region Moderation

    static void CreateModerationIndexes(IMongoDatabase db)
    {
        var reports = db.GetCollection<ContentReport>("content_reports");

        // For showing pending reports to moderators
        reports.Indexes.CreateOne(new CreateIndexModel<ContentReport>(
            Builders<ContentReport>.IndexKeys
                .Ascending(r => r.Status)
                .Descending(r => r.CreatedAt),
            new CreateIndexOptions { Name = "ix_reports_status_created" }
        ));

        // For finding all reports about a specific piece of content
        reports.Indexes.CreateOne(new CreateIndexModel<ContentReport>(
            Builders<ContentReport>.IndexKeys
                .Ascending(r => r.TargetType)
                .Ascending(r => r.TargetId),
            new CreateIndexOptions { Name = "ix_reports_target" }
        ));

        // For identifying users who are frequently reported
        reports.Indexes.CreateOne(new CreateIndexModel<ContentReport>(
            Builders<ContentReport>.IndexKeys
                .Ascending(r => r.TargetAuthorId)
                .Descending(r => r.CreatedAt),
            new CreateIndexOptions { Name = "ix_reports_author_created" }
        ));

        // For tracking a moderator's review history
        reports.Indexes.CreateOne(new CreateIndexModel<ContentReport>(
            Builders<ContentReport>.IndexKeys.Ascending(r => r.ReviewedByModeratorId),
            new CreateIndexOptions { Name = "ix_reports_reviewer" }
        ));

        var actions = db.GetCollection<ModerationAction>("moderation_actions");

        // For finding all moderation actions on a specific piece of content
        actions.Indexes.CreateOne(new CreateIndexModel<ModerationAction>(
            Builders<ModerationAction>.IndexKeys
                .Ascending(a => a.TargetType)
                .Ascending(a => a.TargetId),
            new CreateIndexOptions { Name = "ix_moderation_target" }
        ));

        // For tracking a moderator's action history
        actions.Indexes.CreateOne(new CreateIndexModel<ModerationAction>(
            Builders<ModerationAction>.IndexKeys
                .Ascending(a => a.ModeratorId)
                .Descending(a => a.CreatedAt),
            new CreateIndexOptions { Name = "ix_moderation_moderator_created" }
        ));
    }

    #endregion

    #region Validators

    static void EnsureIssueValidator(IMongoDatabase db)
    {
        var validator = new BsonDocument
        {
            { "$jsonSchema", new BsonDocument
                {
                    { "bsonType", "object" },
                    { "required", new BsonArray { "title", "description", "location", "cityId", "reporter" } },
                    { "properties", new BsonDocument
                        {
                            { "title", new BsonDocument { { "bsonType", "string" } } },
                            { "description", new BsonDocument { { "bsonType", "string" } } },
                            { "location", new BsonDocument { { "bsonType", "object" } } },
                            { "cityId", new BsonDocument { { "bsonType", "objectId" } } },
                            { "reporter", new BsonDocument 
                                { 
                                    { "bsonType", "object" },
                                    { "required", new BsonArray { "id", "displayName" } },
                                    { "properties", new BsonDocument 
                                        {
                                            { "id", new BsonDocument { { "bsonType", "objectId" } } },
                                            { "displayName", new BsonDocument { { "bsonType", "string" } } }
                                        }
                                    }
                                } 
                            }
                        }
                    }
                }
            }
        };

        try
        {
            db.RunCommand<BsonDocument>(new BsonDocument
            {
                { "collMod", "issues" },
                { "validator", validator },
                { "validationLevel", "moderate" }
            });
        }
        catch (MongoCommandException ex) when (ex.CodeName == "NamespaceNotFound")
        {
            db.RunCommand<BsonDocument>(new BsonDocument
            {
                { "create", "issues" },
                { "validator", validator }
            });
        }
    }

    #endregion
}