using AspNetCore.Identity.Mongo.Model;
using FixIt.Models.Enums;
using FixIt.Models.Users;
using FixIt.Services.Constants;
using Microsoft.AspNetCore.Identity;

namespace FixIt.Extensions;

/// <summary>
/// Single chokepoint for changing a user's role. The codebase keeps two role
/// representations that must stay in sync: <c>ApplicationUser.Role</c> (an
/// enum on the user document, used by some inline checks and views) and the
/// Identity role store (read by <c>RequireRole</c> policies and JWT-claim
/// generation). The Phase 1 admin-login bug was a classic drift between them.
///
/// All role mutations should go through <see cref="SetUserRoleAsync"/>. Direct
/// writes to <c>user.Role</c> or <c>userManager.AddToRoleAsync</c> bypass this
/// invariant and reintroduce the same bug class.
/// </summary>
public static class UserRoleSync
{
    public sealed record SetRoleResult(bool Ok, string Message, IReadOnlyList<string> Errors)
    {
        public static SetRoleResult Success(string message) => new(true, message, Array.Empty<string>());
        public static SetRoleResult Failure(string message, IEnumerable<string>? errors = null)
            => new(false, message, errors?.ToList() ?? new List<string>());
    }

    public static async Task<SetRoleResult> SetUserRoleAsync(
        IServiceProvider services,
        ApplicationUser user,
        UserRole desiredRole,
        ILogger logger,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<MongoRole>>();

        var managedRoles = new[] { RoleNames.Admin, RoleNames.Moderator };

        var existingRoles = await userManager.GetRolesAsync(user);
        var rolesToRemove = existingRoles.Intersect(managedRoles, StringComparer.Ordinal).ToList();
        if (rolesToRemove.Count > 0)
        {
            var removeResult = await userManager.RemoveFromRolesAsync(user, rolesToRemove);
            if (!removeResult.Succeeded)
            {
                return SetRoleResult.Failure(
                    "Failed to remove existing managed roles.",
                    removeResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
            }
        }

        var targetRoleName = desiredRole switch
        {
            UserRole.Admin => RoleNames.Admin,
            UserRole.Moderator => RoleNames.Moderator,
            UserRole.User => null,
            _ => null,
        };

        if (targetRoleName != null)
        {
            if (!await roleManager.RoleExistsAsync(targetRoleName))
            {
                var roleCreate = await roleManager.CreateAsync(new MongoRole(targetRoleName));
                if (!roleCreate.Succeeded)
                {
                    return SetRoleResult.Failure(
                        $"Failed to create '{targetRoleName}' role in Identity store.",
                        roleCreate.Errors.Select(e => $"{e.Code}: {e.Description}"));
                }
            }

            var addResult = await userManager.AddToRoleAsync(user, targetRoleName);
            if (!addResult.Succeeded)
            {
                return SetRoleResult.Failure(
                    $"Failed to add '{targetRoleName}' role to user.",
                    addResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
            }
        }

        // Keep the denormalized enum in sync with the authoritative claim store.
        // We update last so a mid-operation crash leaves the user with no
        // managed role + enum=User (which is the safe state — fewer privileges).
        if (user.Role != desiredRole)
        {
            user.Role = desiredRole;
            var updateResult = await userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                return SetRoleResult.Failure(
                    "Role claim updated but failed to persist enum on user document. " +
                    "Run the role-drift audit (see Program.cs startup) to confirm consistency.",
                    updateResult.Errors.Select(e => $"{e.Code}: {e.Description}"));
            }
        }

        logger.LogInformation(
            "User role set: {UserId} ({Email}) → {Role}",
            user.Id, user.Email, desiredRole);

        return SetRoleResult.Success($"Role updated to {desiredRole}.");
    }

    /// <summary>
    /// Walks every user, comparing <c>ApplicationUser.Role</c> against the
    /// Identity role-claim store, logging warnings for any mismatch. Intended
    /// for one-shot startup audit and ops diagnostics, not request-path use.
    /// </summary>
    public static async Task<int> AuditRoleDriftAsync(
        IServiceProvider services,
        ILogger logger,
        CancellationToken ct = default)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        int driftCount = 0;
        int checkedCount = 0;

        foreach (var user in userManager.Users.ToList())
        {
            ct.ThrowIfCancellationRequested();
            checkedCount++;

            var roles = await userManager.GetRolesAsync(user);

            var enumImpliesAdmin = user.Role == UserRole.Admin;
            var enumImpliesModerator = user.Role == UserRole.Moderator;
            var claimSaysAdmin = roles.Contains(RoleNames.Admin, StringComparer.Ordinal);
            var claimSaysModerator = roles.Contains(RoleNames.Moderator, StringComparer.Ordinal);

            if (enumImpliesAdmin != claimSaysAdmin || enumImpliesModerator != claimSaysModerator)
            {
                driftCount++;
                logger.LogWarning(
                    "Role drift on user {UserId} ({Email}): enum={Enum}, role-claims=[{Claims}]. Reconcile via UserRoleSync.SetUserRoleAsync.",
                    user.Id, user.Email, user.Role, string.Join(",", roles));
            }
        }

        logger.LogInformation(
            "Role-drift audit complete: checked {Checked}, mismatches {Drift}",
            checkedCount, driftCount);

        return driftCount;
    }
}
