using AspNetCore.Identity.Mongo.Model;
using FixIt.Models.Enums;
using FixIt.Models.Users;
using FixIt.Services.Constants;
using Microsoft.AspNetCore.Identity;

namespace FixIt.Extensions;

/// <summary>
/// Admin user bootstrapping. Backs both the development seeder (config-driven,
/// auto-runs on empty DB) and the CLI subcommand (manual, works in any env).
/// Both paths route through <see cref="EnsureAdminAsync"/> so role wiring stays
/// consistent — the user document's <c>Role</c> enum and the Identity role store
/// must agree for authorization policies to grant access.
/// </summary>
public static class AdminBootstrapExtensions
{
    public sealed record EnsureAdminResult(bool Ok, string Message, IReadOnlyList<string> Errors)
    {
        public static EnsureAdminResult Success(string message) => new(true, message, Array.Empty<string>());
        public static EnsureAdminResult Failure(string message, IEnumerable<string>? errors = null)
            => new(false, message, errors?.ToList() ?? new List<string>());
    }

    public static async Task<EnsureAdminResult> EnsureAdminAsync(
        IServiceProvider services,
        string email,
        string userName,
        string displayName,
        string password,
        bool force,
        ILogger logger,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return EnsureAdminResult.Failure("Email is required.");
        if (string.IsNullOrWhiteSpace(password))
            return EnsureAdminResult.Failure("Password is required.");

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<MongoRole>>();

        if (!await roleManager.RoleExistsAsync(RoleNames.Admin))
        {
            var roleCreate = await roleManager.CreateAsync(new MongoRole(RoleNames.Admin));
            if (!roleCreate.Succeeded)
            {
                return EnsureAdminResult.Failure(
                    "Failed to create Admin role in Identity role store.",
                    roleCreate.Errors.Select(e => $"{e.Code}: {e.Description}"));
            }
        }

        var existing = await userManager.FindByEmailAsync(email);
        if (existing != null)
        {
            if (!force)
            {
                return EnsureAdminResult.Failure(
                    $"A user with email '{email}' already exists. Re-run with --force to overwrite.");
            }

            logger.LogWarning("Force flag set — deleting existing user {Email} before recreating as admin.", email);
            var delete = await userManager.DeleteAsync(existing);
            if (!delete.Succeeded)
            {
                return EnsureAdminResult.Failure(
                    "Failed to delete existing user for force-recreate.",
                    delete.Errors.Select(e => $"{e.Code}: {e.Description}"));
            }
        }

        var newAdmin = new ApplicationUser
        {
            UserName = string.IsNullOrWhiteSpace(userName) ? email : userName,
            Email = email,
            EmailConfirmed = true,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Admin" : displayName,
            Role = UserRole.Admin,
            TwoFactorEnabled = false,
        };

        var create = await userManager.CreateAsync(newAdmin, password);
        if (!create.Succeeded)
        {
            return EnsureAdminResult.Failure(
                "Failed to create admin user.",
                create.Errors.Select(e => $"{e.Code}: {e.Description}"));
        }

        var addRole = await userManager.AddToRoleAsync(newAdmin, RoleNames.Admin);
        if (!addRole.Succeeded)
        {
            // The user exists but lacks the role claim — surface this loudly so
            // ops doesn't leave a half-provisioned account in the DB.
            return EnsureAdminResult.Failure(
                "Created user but failed to assign Admin role. Manual cleanup required.",
                addRole.Errors.Select(e => $"{e.Code}: {e.Description}"));
        }

        logger.LogInformation("Admin user provisioned: {Email} ({UserId})", email, newAdmin.Id);
        return EnsureAdminResult.Success($"Admin user '{email}' created and assigned the Admin role.");
    }

    /// <summary>
    /// CLI entry point. Usage:
    ///   dotnet run -- --bootstrap-admin --email &lt;e&gt; --password &lt;p&gt;
    ///                 [--username &lt;u&gt;] [--display-name &lt;n&gt;] [--force]
    /// Returns a process exit code: 0 success, 1 bad usage, 2 user already exists,
    /// 3 identity failure.
    /// </summary>
    public static async Task<int> RunAdminBootstrapAsync(this WebApplication app, string[] args)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        if (!TryParseArgs(args, out var parsed, out var usageError))
        {
            Console.Error.WriteLine($"[bootstrap-admin] {usageError}");
            Console.Error.WriteLine(UsageText);
            return 1;
        }

        using var scope = app.Services.CreateScope();
        var result = await EnsureAdminAsync(
            scope.ServiceProvider,
            parsed.Email,
            parsed.UserName,
            parsed.DisplayName,
            parsed.Password,
            parsed.Force,
            logger);

        if (result.Ok)
        {
            Console.WriteLine($"[bootstrap-admin] {result.Message}");
            return 0;
        }

        Console.Error.WriteLine($"[bootstrap-admin] {result.Message}");
        foreach (var err in result.Errors)
        {
            Console.Error.WriteLine($"  - {err}");
        }

        return result.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase) ? 2 : 3;
    }

    private sealed record ParsedArgs(string Email, string Password, string UserName, string DisplayName, bool Force);

    private const string UsageText =
        "Usage: dotnet run -- --bootstrap-admin --email <email> --password <password> " +
        "[--username <username>] [--display-name <name>] [--force]";

    private static bool TryParseArgs(string[] args, out ParsedArgs parsed, out string error)
    {
        string? email = null, password = null, userName = null, displayName = null;
        var force = false;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (string.Equals(a, "--bootstrap-admin", StringComparison.OrdinalIgnoreCase))
                continue;
            if (string.Equals(a, "--force", StringComparison.OrdinalIgnoreCase))
            {
                force = true;
                continue;
            }

            // Support both `--flag value` and `--flag=value`.
            string flag = a, value;
            var eq = a.IndexOf('=');
            if (eq > 0)
            {
                flag = a[..eq];
                value = a[(eq + 1)..];
            }
            else if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
            {
                value = args[++i];
            }
            else
            {
                parsed = default!;
                error = $"Missing value for argument '{flag}'.";
                return false;
            }

            switch (flag.ToLowerInvariant())
            {
                case "--email": email = value; break;
                case "--password": password = value; break;
                case "--username": userName = value; break;
                case "--display-name": displayName = value; break;
                default:
                    parsed = default!;
                    error = $"Unknown argument '{flag}'.";
                    return false;
            }
        }

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            parsed = default!;
            error = "Both --email and --password are required.";
            return false;
        }

        parsed = new ParsedArgs(email!, password!, userName ?? string.Empty, displayName ?? string.Empty, force);
        error = string.Empty;
        return true;
    }

    public static bool IsBootstrapRequested(string[] args) =>
        args.Any(a => string.Equals(a, "--bootstrap-admin", StringComparison.OrdinalIgnoreCase));
}
