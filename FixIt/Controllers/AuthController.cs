using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using FixIt.Services.Authentication;
using FixIt.Models.Users;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace FixIt.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IOAuthService _oauthService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IOAuthService oauthService,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AuthController> logger)
    {
        _oauthService = oauthService;
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
    }

    /// <summary>
    /// Initiates OAuth login with specified provider
    /// </summary>
    [HttpPost("login/{provider}")]
    public IActionResult SignInWithProvider(string provider)
    {
        // Validate provider
        if (!new[] { "Google", "GitHub", "Microsoft" }.Contains(provider, StringComparer.OrdinalIgnoreCase))
        {
            return BadRequest("Invalid provider");
        }

        var authProperties = new AuthenticationProperties
        {
            RedirectUri = Url.Action("SignInCallback", new { provider })
        };

        return Challenge(authProperties, provider);
    }

    /// <summary>
    /// Handles OAuth callback after user authenticates with provider
    /// </summary>
    [HttpGet("signin-callback")]
    public async Task<IActionResult> SignInCallback(string provider)
    {
        var result = await HttpContext.AuthenticateAsync(provider);
        if (!result.Succeeded)
        {
            _logger.LogWarning($"Failed to authenticate with {provider}");
            return Redirect("/login?error=authentication_failed");
        }

        var principal = result.Principal;
        var providerUserId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(providerUserId))
        {
            _logger.LogWarning($"Could not extract provider ID from {provider}");
            return Redirect("/login?error=invalid_provider_response");
        }

        // Get or create user from external login
        var user = await _oauthService.GetOrCreateUserFromExternalLoginAsync(provider, providerUserId, principal!);
        if (user == null)
        {
            _logger.LogWarning($"Failed to create user from {provider} login");
            return Redirect("/login?error=user_creation_failed");
        }

        // Sign in the user with custom claims
        var claims = new List<Claim>
        {
            new Claim("DisplayName", user.DisplayName),
            new Claim(ClaimTypes.Email, user.Email ?? "")
        };
        
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var customPrincipal = new ClaimsPrincipal(identity);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, customPrincipal);
        
        await _oauthService.UpdateLastSignInAsync(user, provider);

        _logger.LogInformation($"User {user.Email} signed in via {provider}");
        return Redirect("/");
    }

    /// <summary>
    /// Signs out the current user
    /// </summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Redirect("/");
    }

    /// <summary>
    /// Gets current user info
    /// </summary>
    [HttpGet("user")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Ok(new { authenticated = false });
        }

        return Ok(new
        {
            authenticated = true,
            id = user.Id,
            email = user.Email,
            displayName = user.DisplayName,
            role = user.Role.ToString(),
            externalProviders = user.ExternalIdentities.Select(e => new
            {
                provider = e.Provider,
                displayName = e.ProviderDisplayName,
                connectedAt = e.ConnectedAt,
                lastSignInAt = e.LastSignInAt
            })
        });
    }

    /// <summary>
    /// Links additional OAuth provider to existing account
    /// </summary>
    [HttpPost("link-provider/{provider}")]
    public IActionResult LinkProvider(string provider)
    {
        if (!User.Identity?.IsAuthenticated ?? false)
        {
            return Unauthorized("User must be authenticated");
        }

        var authProperties = new AuthenticationProperties
        {
            RedirectUri = Url.Action("LinkProviderCallback", new { provider })
        };

        return Challenge(authProperties, provider);
    }

    /// <summary>
    /// Handles provider linking callback
    /// </summary>
    [HttpGet("link-callback")]
    public async Task<IActionResult> LinkProviderCallback(string provider)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized("User not found");
        }

        var result = await HttpContext.AuthenticateAsync(provider);
        if (!result.Succeeded)
        {
            return Redirect($"/settings/connected-accounts?error=linking_failed");
        }

        var principal = result.Principal;
        var providerUserId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(providerUserId))
        {
            return Redirect($"/settings/connected-accounts?error=invalid_response");
        }

        // Link the external identity
        await _oauthService.LinkExternalIdentityAsync(user, provider, providerUserId, principal!);

        _logger.LogInformation($"Linked {provider} to user {user.Email}");
        return Redirect("/settings/connected-accounts?success=linked");
    }

    /// <summary>
    /// Unlinks OAuth provider from user account
    /// </summary>
    [HttpPost("unlink-provider/{provider}")]
    public async Task<IActionResult> UnlinkProvider(string provider)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized("User not found");
        }

        // Ensure user has password or other OAuth providers
        var otherProviders = user.ExternalIdentities.Where(e => e.Provider != provider).ToList();
        if (!user.HasPasswordAuth && otherProviders.Count == 0)
        {
            return BadRequest("Cannot unlink the only authentication method. Set up password first.");
        }

        // Remove the external identity
        var identity = user.ExternalIdentities.FirstOrDefault(e => e.Provider == provider);
        if (identity != null)
        {
            user.ExternalIdentities.Remove(identity);
            await _userManager.UpdateAsync(user);
            _logger.LogInformation($"Unlinked {provider} from user {user.Email}");
        }

        return Ok(new { success = true });
    }
}
