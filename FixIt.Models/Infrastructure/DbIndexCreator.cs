using MongoDB.Bson;
using MongoDB.Driver;
using FixIt.Models.Users;
using FixIt.Models.Issues;
using FixIt.Models.Engagement;
using FixIt.Models.Notifications;

namespace FixIt.Models.Infrastructure;

public static class DbIndexCreator
{
    public static void EnsureIndexesAndValidators(IMongoDatabase db)
    {
        CreateIdentityIndexes(db);
        CreateIssueIndexes(db);
        CreateEngagementIndexes(db);
        CreateMediaIndexes(db);
        CreateSubscriptionIndexes(db);
        EnsureIssueValidator(db);
    }

    #region Identity

    static void CreateIdentityIndexes(IMongoDatabase db)
    {
        var users = db.GetCollection<ApplicationUser>("AspNetUsers");

        users.Indexes.CreateOne(new CreateIndexModel<ApplicationUser>(
            Builders<ApplicationUser>.IndexKeys.Ascending(u => u.NormalizedUserName),
            new CreateIndexOptions { Unique = true, Name = "ux_users_normalizedusername" }
        ));

        users.Indexes.CreateOne(new CreateIndexModel<ApplicationUser>(
            Builders<ApplicationUser>.IndexKeys.Ascending(u => u.NormalizedEmail),
            new CreateIndexOptions { Name = "ix_users_normalizedemail" }
        ));
    }

    #endregion

    #region Issues

    static void CreateIssueIndexes(IMongoDatabase db)
    {
        var issues = db.GetCollection<Issue>("issues");

        issues.Indexes.CreateOne(new CreateIndexModel<Issue>(
            Builders<Issue>.IndexKeys.Geo2DSphere(i => i.Location),
            new CreateIndexOptions { Name = "idx_issues_location_2dsphere" }
        ));

        issues.Indexes.CreateOne(new CreateIndexModel<Issue>(
            Builders<Issue>.IndexKeys
                .Text(i => i.Title)
                .Text(i => i.Description)
                .Text("Tags"),
            new CreateIndexOptions { Name = "idx_issues_text" }
        ));

        issues.Indexes.CreateOne(new CreateIndexModel<Issue>(
            Builders<Issue>.IndexKeys
                .Ascending(i => i.CityId)
                .Ascending(i => i.Status)
                .Descending(i => i.CreatedAt),
            new CreateIndexOptions { Name = "idx_issues_city_status_created" }
        ));

        issues.Indexes.CreateOne(new CreateIndexModel<Issue>(
            Builders<Issue>.IndexKeys.Ascending("Reporter.Id"),
            new CreateIndexOptions { Name = "idx_issues_reporter_id" }
        ));

        CreatePartialRecentIssuesIndex(db);
    }

    static void CreatePartialRecentIssuesIndex(IMongoDatabase db)
    {
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

    #endregion

    #region Engagement

    static void CreateEngagementIndexes(IMongoDatabase db)
    {
        var votes = db.GetCollection<Vote>("votes");

        votes.Indexes.CreateOne(new CreateIndexModel<Vote>(
            Builders<Vote>.IndexKeys
                .Ascending(v => v.IssueId)
                .Ascending(v => v.UserId),
            new CreateIndexOptions { Unique = true, Name = "ux_votes_issue_user" }
        ));

        var comments = db.GetCollection<Comment>("comments");

        comments.Indexes.CreateOne(new CreateIndexModel<Comment>(
            Builders<Comment>.IndexKeys
                .Ascending(c => c.IssueId)
                .Descending(c => c.CreatedAt),
            new CreateIndexOptions { Name = "idx_comments_issue_created" }
        ));

        comments.Indexes.CreateOne(new CreateIndexModel<Comment>(
            Builders<Comment>.IndexKeys.Ascending(c => c.AuthorId),
            new CreateIndexOptions { Name = "idx_comments_author" }
        ));
    }

    #endregion

    #region Media

    static void CreateMediaIndexes(IMongoDatabase db)
    {
        var media = db.GetCollection<FixIt.Models.Media.Media>("media");

        media.Indexes.CreateOne(new CreateIndexModel<FixIt.Models.Media.Media>(
            Builders<FixIt.Models.Media.Media>.IndexKeys
                .Ascending(m => m.OwnerId)
                .Descending(m => m.CreatedAt),
            new CreateIndexOptions { Name = "idx_media_owner_created" }
        ));
    }

    #endregion

    #region Notifications

    static void CreateSubscriptionIndexes(IMongoDatabase db)
    {
        var subs = db.GetCollection<NotificationSubscription>("subscriptions");

        subs.Indexes.CreateOne(new CreateIndexModel<NotificationSubscription>(
            Builders<NotificationSubscription>.IndexKeys.Geo2DSphere(s => s.Center),
            new CreateIndexOptions { Name = "idx_subscriptions_center_2dsphere" }
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