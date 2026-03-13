using FixIt.Models.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace FixIt.Services.Authentication;

/// <summary>
/// Handles OAuth authentication callbacks and external identity linking
/// </summary>
public interface IOAuthService
{
    Task<ApplicationUser?> GetOrCreateUserFromExternalLoginAsync(
        string provider, string providerId, ClaimsPrincipal principal);
    
    Task LinkExternalIdentityAsync(
        ApplicationUser user, string provider, string providerId, ClaimsPrincipal principal);
    
    Task UpdateLastSignInAsync(ApplicationUser user, string provider);
    
    Task<ExternalIdentity?> FindExternalIdentityAsync(string provider, string providerId);
}

public class OAuthService : IOAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<OAuthService> _logger;

    public OAuthService(
        UserManager<ApplicationUser> userManager,
        ILogger<OAuthService> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Gets existing user or creates new one from external OAuth login
    /// </summary>
    public async Task<ApplicationUser?> GetOrCreateUserFromExternalLoginAsync(
        string provider, string providerId, ClaimsPrincipal principal)
    {
        // Check if user already linked to this external identity
        var existingIdentity = await FindExternalIdentityAsync(provider, providerId);
        if (existingIdentity != null)
        {
            var user = await _userManager.FindByEmailAsync(
                principal.FindFirstValue(ClaimTypes.Email) ?? "");
            return user;
        }

        // Extract user info from OAuth provider claims
        var email = principal.FindFirstValue(ClaimTypes.Email);
        var displayName = principal.FindFirstValue(ClaimTypes.Name) ?? 
                         principal.FindFirstValue("urn:github:login") ?? 
                         email?.Split('@')[0] ?? "User";

        // Generate a unique username
        var baseUsername = principal.FindFirstValue("urn:github:login") ?? 
                          email?.Split('@')[0] ?? 
                          displayName.ToLower().Replace(" ", "");
        
        var username = await GenerateUniqueUsernameAsync(baseUsername);

        // Check if user exists by email
        var existingUser = email != null ? await _userManager.FindByEmailAsync(email) : null;

        if (existingUser != null)
        {
            // Link external identity to existing user
            await LinkExternalIdentityAsync(existingUser, provider, providerId, principal);
            _logger.LogInformation($"Linked {provider} identity to existing user {existingUser.Email}");
            return existingUser;
        }

        // Create new user
        var newUser = new ApplicationUser
        {
            UserName = username,
            Email = email,
            DisplayName = displayName,
            EmailConfirmed = email != null,
            HasPasswordAuth = false, // OAuth users don't need password
            ExternalIdentities = new List<ExternalIdentity>
            {
                new ExternalIdentity
                {
                    Provider = provider,
                    ProviderId = providerId,
                    ProviderUsername = principal.FindFirstValue("urn:github:login"),
                    ProviderDisplayName = displayName,
                    ConnectedAt = DateTime.UtcNow,
                    LastSignInAt = DateTime.UtcNow
                }
            }
        };

        var result = await _userManager.CreateAsync(newUser);
        if (!result.Succeeded)
        {
            _logger.LogError($"Failed to create user from {provider} login: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            return null;
        }

        // Assign default user role
        await _userManager.AddToRoleAsync(newUser, "User");

        _logger.LogInformation($"Created new user from {provider} login: {newUser.Email}");
        return newUser;
    }

    /// <summary>
    /// Links an external OAuth identity to existing user account
    /// </summary>
    public async Task LinkExternalIdentityAsync(
        ApplicationUser user, string provider, string providerId, ClaimsPrincipal principal)
    {
        // Check if already linked
        if (user.ExternalIdentities.Any(e => e.Provider == provider && e.ProviderId == providerId))
        {
            return;
        }

        var displayName = principal.FindFirstValue(ClaimTypes.Name) ?? 
                         principal.FindFirstValue("urn:github:login") ?? "User";

        user.ExternalIdentities.Add(new ExternalIdentity
        {
            Provider = provider,
            ProviderId = providerId,
            ProviderUsername = principal.FindFirstValue("urn:github:login"),
            ProviderDisplayName = displayName,
            ConnectedAt = DateTime.UtcNow,
            LastSignInAt = DateTime.UtcNow
        });

        await _userManager.UpdateAsync(user);
        _logger.LogInformation($"Linked {provider} identity to user {user.Email}");
    }

    /// <summary>
    /// Updates the last sign-in time for an external identity
    /// </summary>
    public async Task UpdateLastSignInAsync(ApplicationUser user, string provider)
    {
        var identity = user.ExternalIdentities.FirstOrDefault(e => e.Provider == provider);
        if (identity != null)
        {
            identity.LastSignInAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
        }
    }

    /// <summary>
    /// Generates a unique username, appending a number if necessary
    /// </summary>
    private async Task<string> GenerateUniqueUsernameAsync(string baseUsername)
    {
        // Clean up the username (remove special characters, convert to lowercase)
        var cleanUsername = System.Text.RegularExpressions.Regex.Replace(
            baseUsername.ToLower(), 
            @"[^a-z0-9_-]", 
            string.Empty);
        
        cleanUsername = string.IsNullOrEmpty(cleanUsername) ? "user" : cleanUsername.Substring(0, Math.Min(20, cleanUsername.Length));

        // Check if username is available
        var existingUser = await _userManager.FindByNameAsync(cleanUsername);
        if (existingUser == null)
        {
            return cleanUsername;
        }

        // If taken, append numbers until we find an available username
        for (int i = 1; i <= 9999; i++)
        {
            var candidateUsername = $"{cleanUsername}{i}";
            existingUser = await _userManager.FindByNameAsync(candidateUsername);
            if (existingUser == null)
            {
                return candidateUsername;
            }
        }

        // Fallback to GUID-based username (extremely unlikely)
        return Guid.NewGuid().ToString().Substring(0, 20);
    }

    /// <summary>
    /// Finds external identity by provider and provider ID
    /// </summary>
    public Task<ExternalIdentity?> FindExternalIdentityAsync(string provider, string providerId)
    {
        // This is a simplified approach - in production, you might want to index this in MongoDB
        // For now, we're searching through all users (should add a query method to UserManager)
        var allUsers = _userManager.Users.ToList();
        var user = allUsers.FirstOrDefault(u => 
            u.ExternalIdentities.Any(e => e.Provider == provider && e.ProviderId == providerId));
        
        return Task.FromResult(
            user?.ExternalIdentities.FirstOrDefault(e => e.Provider == provider && e.ProviderId == providerId)
        );
    }
}
