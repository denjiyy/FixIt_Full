using MongoDB.Driver;

namespace FixIt.Data.Configuration.Contracts;

public interface ICollectionConfigurator
{
    Task ConfigureAsync(IMongoDatabase db);
}
