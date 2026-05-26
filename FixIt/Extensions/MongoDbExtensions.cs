using FixIt.Data.Infrastructure;
using FixIt.Data.Infrastructure.Migrations;
using FixIt.Data.Repository;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Infrastructure;
using MongoDB.Driver;

namespace FixIt.Extensions;

/// <summary>
/// Mongo client, database, and repository registrations. Keeps the connection
/// resolution centralised so other extensions (Identity, etc.) can reuse the
/// same settings without duplicating placeholder logic.
/// </summary>
public static class MongoDbExtensions
{
    public static (IServiceCollection Services, string ConnectionString, string DatabaseName) AddMongoDb(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var (connectionString, databaseName) = StartupConfigurationHelpers.ResolveMongoSettings(configuration);

        services.AddSingleton<IMongoClient>(_ => new MongoClient(connectionString));
        services.AddSingleton<IMongoDatabase>(sp =>
            sp.GetRequiredService<IMongoClient>().GetDatabase(databaseName));
        services.AddSingleton<MongoDbContext>(sp =>
            new MongoDbContext(sp.GetRequiredService<IMongoClient>(), databaseName));

        services.Configure<MongoDbSettings>(configuration.GetSection(MongoDbSettings.SectionName));
        services.AddScoped<MigrationRunner>();

        RegisterRepositories(services);

        return (services, connectionString, databaseName);
    }

    private static void RegisterRepositories(IServiceCollection services)
    {
        services.AddScoped<IRepository<FixIt.Models.Locations.City>>(sp =>
            new Repository<FixIt.Models.Locations.City>(sp.GetRequiredService<IMongoDatabase>(), MongoCollectionNames.Cities));

        services.AddScoped<IRepository<FixIt.Models.Locations.Neighborhood>>(sp =>
            new Repository<FixIt.Models.Locations.Neighborhood>(sp.GetRequiredService<IMongoDatabase>(), MongoCollectionNames.Neighborhoods));

        services.AddScoped<IRepository<FixIt.Models.Issues.Tag>>(sp =>
            new Repository<FixIt.Models.Issues.Tag>(sp.GetRequiredService<IMongoDatabase>(), MongoCollectionNames.Tags));

        services.AddScoped<IRepository<FixIt.Models.Issues.Issue>>(sp =>
            new Repository<FixIt.Models.Issues.Issue>(sp.GetRequiredService<IMongoDatabase>(), MongoCollectionNames.Issues));

        services.AddScoped<IRepository<FixIt.Models.Engagement.Vote>>(sp =>
            new Repository<FixIt.Models.Engagement.Vote>(sp.GetRequiredService<IMongoDatabase>(), MongoCollectionNames.Votes));

        services.AddScoped<IRepository<FixIt.Models.Issues.ViewEvent>>(sp =>
            new Repository<FixIt.Models.Issues.ViewEvent>(sp.GetRequiredService<IMongoDatabase>(), MongoCollectionNames.ViewEvents));

        services.AddScoped<IRepository<FixIt.Models.Gamification.UserReputation>>(sp =>
            new Repository<FixIt.Models.Gamification.UserReputation>(sp.GetRequiredService<IMongoDatabase>(), MongoCollectionNames.UserReputations));

        services.AddScoped<IRepository<FixIt.Models.Gamification.ReputationTransaction>>(sp =>
            new Repository<FixIt.Models.Gamification.ReputationTransaction>(sp.GetRequiredService<IMongoDatabase>(), MongoCollectionNames.ReputationTransactions));

        services.AddScoped<IRepository<FixIt.Models.Gamification.LeaderboardEntry>>(sp =>
            new Repository<FixIt.Models.Gamification.LeaderboardEntry>(sp.GetRequiredService<IMongoDatabase>(), MongoCollectionNames.Leaderboards));

        services.AddScoped<IRepository<FixIt.Models.Safety.Hazard>>(sp =>
            new Repository<FixIt.Models.Safety.Hazard>(sp.GetRequiredService<IMongoDatabase>(), MongoCollectionNames.Hazards));

        services.AddScoped<IRepository<FixIt.Models.Users.ApplicationUser>>(sp =>
            new Repository<FixIt.Models.Users.ApplicationUser>(sp.GetRequiredService<IMongoDatabase>(), MongoCollectionNames.Users));

        services.AddScoped<IRepository<FixIt.Models.AI.IssueAnalysis>>(sp =>
            new Repository<FixIt.Models.AI.IssueAnalysis>(sp.GetRequiredService<IMongoDatabase>(), MongoCollectionNames.IssueAnalyses));

        services.AddScoped<IRepository<FixIt.Models.AI.AdminSuggestion>>(sp =>
            new Repository<FixIt.Models.AI.AdminSuggestion>(sp.GetRequiredService<IMongoDatabase>(), MongoCollectionNames.AdminSuggestions));

        services.AddScoped<IRepository<FixIt.Models.Media.Media>>(sp =>
            new Repository<FixIt.Models.Media.Media>(sp.GetRequiredService<IMongoDatabase>(), MongoCollectionNames.Media));

        services.AddScoped<IRepository<FixIt.Models.Media.MediaReference>>(sp =>
            new Repository<FixIt.Models.Media.MediaReference>(sp.GetRequiredService<IMongoDatabase>(), MongoCollectionNames.MediaReferences));

        services.AddScoped<IRepository<FixIt.Models.Engagement.Comment>>(sp =>
            new Repository<FixIt.Models.Engagement.Comment>(sp.GetRequiredService<IMongoDatabase>(), MongoCollectionNames.Comments));

        services.AddScoped<IRepository<FixIt.Models.Moderation.ContentReport>>(sp =>
            new Repository<FixIt.Models.Moderation.ContentReport>(sp.GetRequiredService<IMongoDatabase>(), MongoCollectionNames.ContentReports));

        services.AddScoped<IRepository<FixIt.Models.Transparency.IssueResolutionEvidence>>(sp =>
            new Repository<FixIt.Models.Transparency.IssueResolutionEvidence>(sp.GetRequiredService<IMongoDatabase>(), MongoCollectionNames.IssueResolutionEvidence));

        services.AddScoped<IRepository<FixIt.Models.Accessibility.TranslationRecord>>(sp =>
            new Repository<FixIt.Models.Accessibility.TranslationRecord>(sp.GetRequiredService<IMongoDatabase>(), MongoCollectionNames.Translations));

        services.AddScoped<IRepository<FixIt.Models.Accessibility.SupportedLanguage>>(sp =>
            new Repository<FixIt.Models.Accessibility.SupportedLanguage>(sp.GetRequiredService<IMongoDatabase>(), MongoCollectionNames.SupportedLanguages));
    }
}
