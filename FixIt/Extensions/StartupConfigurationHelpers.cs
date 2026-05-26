namespace FixIt.Extensions;

/// <summary>
/// Helpers shared by the startup-time DI extension methods. Resolves Railway-style
/// "${VAR:default}" placeholder expressions and reads the canonical Mongo
/// connection settings from environment variables and appsettings together.
/// </summary>
internal static class StartupConfigurationHelpers
{
    internal static string? ResolveRailwayStylePlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var trimmed = value.Trim();
        if (!trimmed.Contains("${", StringComparison.Ordinal))
        {
            return value;
        }

        var start = trimmed.IndexOf("${", StringComparison.Ordinal);
        var end = trimmed.IndexOf('}', start + 2);
        if (start < 0 || end < 0)
        {
            return value;
        }

        var placeholderBody = trimmed.Substring(start + 2, end - (start + 2));
        var parts = placeholderBody.Split(':', 2, StringSplitOptions.None);
        var envVarName = parts[0].Trim();
        if (string.IsNullOrWhiteSpace(envVarName))
        {
            return value;
        }

        var envValue = Environment.GetEnvironmentVariable(envVarName);
        if (!string.IsNullOrWhiteSpace(envValue))
        {
            return envValue;
        }

        if (parts.Length == 2)
        {
            return parts[1];
        }

        return value;
    }

    internal static (string ConnectionString, string DatabaseName) ResolveMongoSettings(IConfiguration configuration)
    {
        var connectionString = ResolveRailwayStylePlaceholder(
            Environment.GetEnvironmentVariable("MONGODB_URI")
            ?? configuration["ConnectionStrings:MongoDB"]
            ?? configuration["MongoDB:ConnectionString"]);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "MongoDB connection string not found. Set MONGODB_URI environment variable (or ConnectionStrings:MongoDB / MongoDB:ConnectionString in appsettings.json).");
        }

        if (connectionString.Contains("${", StringComparison.Ordinal) || connectionString.Contains("}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "MongoDB connection string still contains a placeholder expression. " +
                "Railway likely did not substitute MONGODB_URI. " +
                $"Value received: '{connectionString}'.");
        }

        var databaseName = ResolveRailwayStylePlaceholder(
            Environment.GetEnvironmentVariable("MONGODB_DATABASE")
            ?? configuration["MongoDB:DatabaseName"]
            ?? "fixit");

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException(
                "MongoDB database name not found. Set MONGODB_DATABASE environment variable (or MongoDB:DatabaseName in appsettings.json).");
        }

        if (databaseName.Contains("${", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "MongoDB database name still contains a placeholder expression. " +
                "Railway likely did not substitute MONGODB_DATABASE. " +
                $"Value received: '{databaseName}'.");
        }

        return (connectionString, databaseName);
    }
}
