using MongoDB.Driver;

namespace FixIt.Data.Configuration.Contracts;

public interface ICollectionConfigurator
{
    // seedDemoData=false skips sample/demo content (issues, fake authors) while
    // still creating indexes and reference data the running app needs (cities,
    // tags). Production callers pass false so prod never receives sample issues.
    Task ConfigureAsync(IMongoDatabase db, bool seedDemoData);
}
