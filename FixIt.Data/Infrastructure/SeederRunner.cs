using System.Reflection;
using FixIt.Data.Configuration.Contracts;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace FixIt.Data.Infrastructure;

public class SeederRunner
{
    public static async Task RunAllConfiguratorsAsync(IMongoDatabase db, IServiceProvider provider = null!)
    {
        var configs = Assembly.GetExecutingAssembly()
            .GetTypes()
            .Where(t => typeof(ICollectionConfigurator).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
            .Select(t => (ICollectionConfigurator)ActivatorUtilities.CreateInstance(provider ?? new ServiceCollection().BuildServiceProvider(), t))
            .ToList();

        foreach (var c in configs)
        {
            await c.ConfigureAsync(db);
        }
    }
}
