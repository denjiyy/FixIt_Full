using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using FixIt.Extensions;
using FixIt.Services.Authentication;
using FixIt.Services.Contracts;
using FixIt.Models.Users;
using FixIt.ViewModels;
using FixIt.ViewModels.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using FixIt.Services.Constants;
using FixIt.Services.Email;
using FixIt.Areas.Identity;

namespace FixIt.Controllers;

/// <summary>
/// Authentication controller - handles OAuth and user authentication
/// All endpoints return JSON for API/mobile client compatibility
/// Supports both cookie-based (web) and token-based (mobile/API) authentication
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[EnableRateLimiting(RateLimitPolicyNames.AuthStrict)]
public class AuthController : ControllerBase
{
    private readonly IOAuthService _oauthService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ILogger<AuthController> _logger;
    private readonly IAuditService _auditService;
    private readonly ITokenService _tokenService;
    private readonly IEmailService _emailService;

    public AuthController(
        IOAuthService oauthService,
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ILogger<AuthController> logger,
        IAuditService auditService,
        ITokenService tokenService,
        IEmailService emailService)
    {
        _oauthService = oauthService;
        _userManager = userManager;
        _signInManager = signInManager;
        _logger = logger;
        _auditService = auditService;
        _tokenService = tokenService;
        _emailService = emailService;
    }

    /// <summary>
    /// Email/password login for mobile clients
    /// Returns JWT tokens on success
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<TokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<TokenResponse>>> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Email) || string.IsNullOrWhiteSpace(request?.Password))
        {
            return BadRequest(ApiResponse<object>.CreateError("Email and password are required"));
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            _logger.LogWarning("Login attempt for non-existent email: {Email}", request.Email);
            return Unauthorized(ApiResponse<object>.CreateError("Invalid email or password"));
        }

        if (user.IsDeleted)
        {
            _logger.LogWarning("Login attempt for deleted user: {UserId}", user.Id);
            return Unauthorized(ApiResponse<object>.CreateError("User account is not active"));
        }

        if (user.IsBanned)
        {
            return Unauthorized(ApiResponse<object>.CreateError("User account is banned"));
        }

        if (user.IsRestricted && user.RestrictedUntil > DateTime.UtcNow)
        {
            return Unauthorized(ApiResponse<object>.CreateError("User account is restricted"));
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                _logger.LogWarning("User {UserId} is locked out", user.Id);
                return Unauthorized(ApiResponse<object>.CreateError("Account is locked. Please try again later."));
            }
            _logger.LogWarning("Failed password login for user: {UserId}", user.Id);
            return Unauthorized(ApiResponse<object>.CreateError("Invalid email or password"));
        }

        // CheckPasswordSignInAsync does not enforce email confirmation (unlike the
        // web PasswordSignInAsync path), so gate it explicitly when required.
        if (_userManager.Options.SignIn.RequireConfirmedEmail && !await _userManager.IsEmailConfirmedAsync(user))
        {
            _logger.LogInformation("Login blocked for unconfirmed email: {UserId}", user.Id);
            return Unauthorized(ApiResponse<object>.CreateError("Please confirm your email before signing in."));
        }

        var userRoles = await _userManager.GetRolesAsync(user);

        user.LastRefreshTokenIssuedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var accessToken = _tokenService.GenerateAccessToken(user, userRoles);
        var refreshToken = _tokenService.GenerateRefreshToken(user);

        _logger.LogInformation("User {UserId} logged in via password", user.Id);

        var tokenResponse = new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = _tokenService.AccessTokenExpirationMinutes * 60,
            User = new UserTokenInfo
            {
                Id = user.Id.ToString(),
                Email = user.Email ?? "",
                DisplayName = user.DisplayName,
                Roles = userRoles,
                ReputationScore = user.ReputationScore,
                TrustLevel = user.TrustLevel,
                IsVerifiedOfficial = user.IsVerifiedOfficial,
                OfficialTitle = user.OfficialTitle,
                OfficialDepartment = user.OfficialDepartment
            }
        };

        return Ok(ApiResponse<TokenResponse>.CreateSuccess(tokenResponse, "Login successful"));
    }

    /// <summary>
    /// Email/password registration for mobile clients
    /// Creates a new user and returns JWT tokens on success
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<TokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<TokenResponse>>> Register([FromBody] RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Email) || string.IsNullOrWhiteSpace(request?.Password))
        {
            return BadRequest(ApiResponse<object>.CreateError("Email and password are required"));
        }

        if (string.IsNullOrWhiteSpace(request.FullName))
        {
            return BadRequest(ApiResponse<object>.CreateError("Full name is required"));
        }

        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
        {
            return BadRequest(ApiResponse<object>.CreateError("An account with this email already exists"));
        }

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            DisplayName = request.FullName,
            HasPasswordAuth = true,
            EmailConfirmed = false
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            _logger.LogWarning("Failed to create user {Email}: {Errors}", request.Email, errors);
            return BadRequest(ApiResponse<object>.CreateError(errors));
        }

        // Everything past CreateAsync runs in a try/catch so any failure rolls back the
        // freshly-created account instead of orphaning the email (which would then 400
        // "account already exists" on retry and block the user permanently).
        try
        {
            // Regular users carry NO managed Identity role: "User" is represented by the
            // ApplicationUser.Role enum (which already defaults to UserRole.User) plus the
            // absence of Admin/Moderator role claims — see UserRoleSync, which deliberately
            // never assigns or seeds a "User" role. Calling AddToRoleAsync(user, "User")
            // here threw "Role User does not exist" (the role is never created), and because
            // the account was already created above, every registration 500'd and orphaned
            // the email. GetRolesAsync returns the (empty) managed-role set for the token.
            var userRoles = await _userManager.GetRolesAsync(user);

            user.LastRefreshTokenIssuedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            var accessToken = _tokenService.GenerateAccessToken(user, userRoles);
            var refreshToken = _tokenService.GenerateRefreshToken(user);

            _logger.LogInformation("New user {UserId} registered via password", user.Id);

            // Issue a confirmation link (verifiable via the web ConfirmEmail page).
            // Isolated so an email-delivery failure never rolls back the account.
            try
            {
                await EmailConfirmationSender.SendAsync(_userManager, _emailService, Url, Request.Scheme, user);
            }
            catch (Exception emailEx)
            {
                _logger.LogError(emailEx, "Failed to send confirmation email for {Email}", request.Email);
            }

            var tokenResponse = new TokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                TokenType = "Bearer",
                ExpiresIn = _tokenService.AccessTokenExpirationMinutes * 60,
                User = new UserTokenInfo
                {
                    Id = user.Id.ToString(),
                    Email = user.Email ?? "",
                    DisplayName = user.DisplayName,
                    Roles = userRoles,
                    ReputationScore = user.ReputationScore,
                    TrustLevel = user.TrustLevel,
                    IsVerifiedOfficial = user.IsVerifiedOfficial,
                    OfficialTitle = user.OfficialTitle,
                    OfficialDepartment = user.OfficialDepartment
                }
            };

            return Ok(ApiResponse<TokenResponse>.CreateSuccess(tokenResponse, "Registration successful"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration failed after account creation for {Email}; rolling back the new user", request.Email);
            try
            {
                await _userManager.DeleteAsync(user);
            }
            catch (Exception cleanupEx)
            {
                _logger.LogError(cleanupEx, "Failed to roll back partially-created user {UserId}", user.Id);
            }

            return StatusCode(StatusCodes.Status500InternalServerError,
                ApiResponse<object>.CreateError("Registration failed. Please try again."));
        }
    }

    /// <summary>
    /// Initiates OAuth login with specified provider
    /// </summary>
    [HttpPost("login/{provider}")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult SignInWithProvider(string provider)
    {
        // Validate provider (only Google is supported)
        if (!provider.Equals("Google", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(ApiResponse<object>.CreateError("Only Google OAuth is supported"));
        }

        var authProperties = new AuthenticationProperties
        {
            RedirectUri = Url.Action("SignInCallback", new { provider })
        };

        return Challenge(authProperties, provider);
    }

    /// <summary>
    /// Handles OAuth callback after user authenticates with provider
    /// Supports both web (redirects) and mobile (JWT tokens) clients via mobile query parameter
    /// Mobile clients will receive JWT access and refresh tokens for subsequent API calls
    /// </summary>
    [HttpGet("signin-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> SignInCallback(string provider, [FromQuery] bool mobile = false)
    {
        var result = await HttpContext.AuthenticateAsync(provider);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed to authenticate with provider {Provider}", provider);
            if (mobile)
            {
                return Ok(ApiResponse<object>.CreateError("Authentication failed"));
            }
            return Redirect("/login?error=authentication_failed");
        }

        var principal = result.Principal;
        var providerUserId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(providerUserId))
        {
            _logger.LogWarning("Could not extract provider ID from provider {Provider}", provider);
            if (mobile)
            {
                return Ok(ApiResponse<object>.CreateError("Invalid provider response"));
            }
            return Redirect("/login?error=invalid_provider_response");
        }

        // Get or create user from external login
        var user = await _oauthService.GetOrCreateUserFromExternalLoginAsync(provider, providerUserId, principal!);
        if (user == null)
        {
            _logger.LogWarning("Failed to create user from provider {Provider} login", provider);
            if (mobile)
            {
                return Ok(ApiResponse<object>.CreateError("User creation failed"));
            }
            return Redirect("/login?error=user_creation_failed");
        }

        if (user.IsDeleted)
        {
            _logger.LogWarning("Blocked sign-in attempt for soft-deleted user {UserId}", user.Id);
            if (mobile)
            {
                return Ok(ApiResponse<object>.CreateError("User account is not active"));
            }
            return Redirect("/login?error=account_inactive");
        }

        var userRoles = await _userManager.GetRolesAsync(user);

        // Establish a complete Identity principal through SignInManager so the
        // auth cookie carries NameIdentifier, roles, email and display name —
        // identical to password sign-in. Previously this built a partial cookie
        // principal by hand, which diverged from the password path and omitted
        // the security stamp. The claims factory owns the custom claims.
        await _signInManager.SignInAsync(user, isPersistent: false);

        await _oauthService.UpdateLastSignInAsync(user, provider);

        // Log admin logins for audit compliance
        if (userRoles.Contains(RoleNames.Admin) || userRoles.Contains(RoleNames.Moderator))
        {
            await _auditService.LogEventAsync(
                eventType: "AdminLogin",
                action: "Login",
                resource: "User",
                resourceId: user.Id.ToString(),
                changes: new Dictionary<string, object>
                {
                    { "UserId", user.Id.ToString() },
                    { "Roles", string.Join(", ", userRoles) },
                    { "Provider", provider }
                }
            );
        }

        _logger.LogInformation("User {UserId} signed in via provider {Provider}", user.Id, provider);

        // Return JWT tokens for mobile/API clients
        if (mobile)
        {
            user.LastRefreshTokenIssuedAt = DateTime.UtcNow;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _logger.LogWarning("Failed to persist token issuance metadata for user {UserId}", user.Id);
            }

            var accessToken = _tokenService.GenerateAccessToken(user, userRoles);
            var refreshToken = _tokenService.GenerateRefreshToken(user);

            var tokenResponse = new TokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                TokenType = "Bearer",
                ExpiresIn = _tokenService.AccessTokenExpirationMinutes * 60,
                User = new UserTokenInfo
                {
                    Id = user.Id.ToString(),
                    Email = user.Email ?? "",
                    DisplayName = user.DisplayName,
                    Roles = userRoles,
                    ReputationScore = user.ReputationScore,
                    TrustLevel = user.TrustLevel,
                    IsVerifiedOfficial = user.IsVerifiedOfficial,
                    OfficialTitle = user.OfficialTitle,
                    OfficialDepartment = user.OfficialDepartment
                }
            };

            return Ok(ApiResponse<TokenResponse>.CreateSuccess(
                tokenResponse,
                "Authentication successful"
            ));
        }

        return Redirect("/");
    }

    /// <summary>
    /// Signs out the current user
    /// Returns JSON response for API/mobile clients
    /// </summary>
    [HttpPost("logout")]
    [ApiAuthorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> Logout()
    {
        var user = await _userManager.GetUserAsync(User);

        if (user != null)
        {
            user.RefreshTokenVersion += 1;
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _logger.LogWarning("Failed to rotate refresh token version during logout for user {UserId}", user.Id);
            }
        }

        await _signInManager.SignOutAsync();

        _logger.LogInformation("User {UserId} logged out", user?.Id.ToString() ?? "unknown");
        
        return Ok(ApiResponse<object>.CreateSuccess(new { message = "Logged out successfully" }, "Logout successful"));
    }

    /// <summary>
    /// Refreshes an expired access token using a refresh token
    /// Used by mobile clients to maintain session without re-authentication
    /// </summary>
    /// <param name="request">Refresh token request containing the refresh token</param>
    /// <returns>New access token wrapped in RefreshTokenResponse</returns>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<RefreshTokenResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<RefreshTokenResponse>>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrEmpty(request?.RefreshToken))
        {
            return BadRequest(ApiResponse<object>.CreateError("Refresh token is required"));
        }

        try
        {
            // Validate the refresh token
            var principal = _tokenService.ValidateToken(request.RefreshToken);
            if (principal == null)
            {
                _logger.LogWarning("Invalid refresh token provided");
                return Unauthorized(ApiResponse<object>.CreateError("Invalid or expired refresh token"));
            }

            var tokenType = principal.FindFirst("TokenType")?.Value;
            if (!string.Equals(tokenType, "refresh", StringComparison.Ordinal))
            {
                _logger.LogWarning("Rejected refresh request with non-refresh token type: {TokenType}", tokenType);
                return Unauthorized(ApiResponse<object>.CreateError("Invalid refresh token"));
            }

            // Extract user ID from refresh token claims
            var userIdClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim?.Value))
            {
                return Unauthorized(ApiResponse<object>.CreateError("Invalid refresh token: missing user information"));
            }

            // Get user from database
            var user = await _userManager.FindByIdAsync(userIdClaim.Value);
            if (user == null)
            {
                _logger.LogWarning("User not found for refresh token");
                return Unauthorized(ApiResponse<object>.CreateError("User not found"));
            }

            if (user.IsDeleted)
            {
                _logger.LogWarning("Rejected refresh token for deleted user {UserId}", user.Id);
                return Unauthorized(ApiResponse<object>.CreateError("User account is not active"));
            }

            // Check if user is banned or restricted
            if (user.IsBanned)
            {
                return Unauthorized(ApiResponse<object>.CreateError("User account is banned"));
            }

            if (user.IsRestricted && user.RestrictedUntil > DateTime.UtcNow)
            {
                return Unauthorized(ApiResponse<object>.CreateError("User account is restricted"));
            }

            var tokenVersionClaim = principal.FindFirst("TokenVersion")?.Value;
            if (!int.TryParse(tokenVersionClaim, out var tokenVersion))
            {
                _logger.LogWarning("Refresh token missing valid token version claim for user {UserId}", user.Id);
                return Unauthorized(ApiResponse<object>.CreateError("Invalid refresh token"));
            }

            if (tokenVersion != user.RefreshTokenVersion)
            {
                _logger.LogWarning(
                    "Refresh token replay/stale token detected for user {UserId}. Token version {TokenVersion}, expected {ExpectedVersion}",
                    user.Id,
                    tokenVersion,
                    user.RefreshTokenVersion);
                return Unauthorized(ApiResponse<object>.CreateError("Invalid or expired refresh token"));
            }

            user.RefreshTokenVersion += 1;
            user.LastRefreshTokenIssuedAt = DateTime.UtcNow;
            var rotateResult = await _userManager.UpdateAsync(user);
            if (!rotateResult.Succeeded)
            {
                _logger.LogWarning("Failed to rotate refresh token version for user {UserId}", user.Id);
                return StatusCode(StatusCodes.Status500InternalServerError,
                    ApiResponse<object>.CreateError("Failed to refresh access token"));
            }

            // Generate new access token
            var userRoles = await _userManager.GetRolesAsync(user);
            var newAccessToken = _tokenService.GenerateAccessToken(user, userRoles);
            var newRefreshToken = _tokenService.GenerateRefreshToken(user);

            var response = new RefreshTokenResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshToken,
                TokenType = "Bearer",
                ExpiresIn = _tokenService.AccessTokenExpirationMinutes * 60
            };

            _logger.LogInformation("Access token refreshed for user {UserId}", user.Id);
            return Ok(ApiResponse<RefreshTokenResponse>.CreateSuccess(
                response,
                "Access token refreshed successfully"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing access token");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                ApiResponse<object>.CreateError("Failed to refresh access token"));
        }
    }

    /// <summary>
    /// Gets current user info
    /// Returns wrapped API response for consistency
    /// </summary>
    [HttpGet("user")]
    [ApiAuthorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> GetCurrentUser()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(ApiResponse<object>.CreateError("User not authenticated"));
        }

        var roles = await _userManager.GetRolesAsync(user);
        return Ok(ApiResponse<object>.CreateSuccess(new
        {
            // ObjectId must be serialized as a string (matching login/register's
            // user.Id.ToString()); returning the raw ObjectId serializes it as an
            // object, which breaks the mobile client's string id parse on the profile.
            id = user.Id.ToString(),
            email = user.Email,
            displayName = user.DisplayName,
            roles = roles,
            externalProviders = user.ExternalIdentities.Select(e => new
            {
                provider = e.Provider,
                displayName = e.ProviderDisplayName,
                connectedAt = e.ConnectedAt,
                lastSignInAt = e.LastSignInAt
            })
        }));
    }

    /// <summary>
    /// Links additional OAuth provider to existing account
    /// For mobile clients, returns authorization URL instead of Challenge
    /// </summary>
    [HttpPost("link-provider/{provider}")]
    [ApiAuthorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public IActionResult LinkProvider(string provider, [FromQuery] bool mobile = false)
    {
        if (!User.Identity?.IsAuthenticated ?? false)
        {
            return Unauthorized(ApiResponse<object>.CreateError("User must be authenticated"));
        }

        var authProperties = new AuthenticationProperties
        {
            RedirectUri = Url.Action("LinkProviderCallback", new { provider, mobile })
        };

        if (mobile)
        {
            // Return authorization URL for mobile client to open in webview
            var authUrl = $"{Request.Scheme}://{Request.Host}/signin-{provider}?redirect_uri={Uri.EscapeDataString(authProperties.RedirectUri ?? string.Empty)}";
            return Ok(ApiResponse<object>.CreateSuccess(
                new { authorizationUrl = authUrl },
                "Open this URL in webview to authorize provider"
            ));
        }

        return Challenge(authProperties, provider);
    }

    /// <summary>
    /// Handles provider linking callback
    /// Supports both web (redirects) and mobile (JSON) clients
    /// </summary>
    [HttpGet("link-callback")]
    [AllowAnonymous]
    public async Task<IActionResult> LinkProviderCallback(string provider, [FromQuery] bool mobile = false)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            if (mobile)
            {
                return Unauthorized(ApiResponse<object>.CreateError("User not found"));
            }
            return Unauthorized();
        }

        var result = await HttpContext.AuthenticateAsync(provider);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed to link provider {Provider} for user {UserId}", provider, user.Id);
            if (mobile)
            {
                return Ok(ApiResponse<object>.CreateError("Provider linking failed"));
            }
            return Redirect($"/settings/connected-accounts?error=linking_failed");
        }

        var principal = result.Principal;
        var providerUserId = principal?.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(providerUserId))
        {
            _logger.LogWarning("Invalid response from provider {Provider}", provider);
            if (mobile)
            {
                return Ok(ApiResponse<object>.CreateError("Invalid provider response"));
            }
            return Redirect($"/settings/connected-accounts?error=invalid_response");
        }

        // Link the external identity
        await _oauthService.LinkExternalIdentityAsync(user, provider, providerUserId, principal!);

        _logger.LogInformation("Linked provider {Provider} to user {UserId}", provider, user.Id);
        
        if (mobile)
        {
            return Ok(ApiResponse<object>.CreateSuccess(
                new { provider, linkedAt = DateTime.UtcNow },
                $"{provider} linked successfully"
            ));
        }
        
        return Redirect("/settings/connected-accounts?success=linked");
    }

    /// <summary>
    /// Unlinks OAuth provider from user account
    /// Returns wrapped API response for consistency
    /// </summary>
    [HttpPost("unlink-provider/{provider}")]
    [ApiAuthorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> UnlinkProvider(string provider)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(ApiResponse<object>.CreateError("User not found"));
        }

        // Ensure user has password or other OAuth providers
        var otherProviders = user.ExternalIdentities.Where(e => e.Provider != provider).ToList();
        if (!user.HasPasswordAuth && otherProviders.Count == 0)
        {
            return BadRequest(ApiResponse<object>.CreateError("Cannot unlink the only authentication method. Set up password first."));
        }

        // Remove the external identity
        var identity = user.ExternalIdentities.FirstOrDefault(e => e.Provider == provider);
        if (identity != null)
        {
            user.ExternalIdentities.Remove(identity);
            await _userManager.UpdateAsync(user);
            _logger.LogInformation("Unlinked provider {Provider} from user {UserId}", provider, user.Id);
        }

        return Ok(ApiResponse<object>.CreateSuccess(
            new { provider, unlinkedAt = DateTime.UtcNow },
            $"{provider} unlinked successfully"
        ));
    }

    /// <summary>
    /// Initiates a password reset for the given email. Always returns success to
    /// avoid leaking whether an account exists (user enumeration). Used by mobile.
    /// </summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> ForgotPassword([FromBody] ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Email))
        {
            return BadRequest(ApiResponse<object>.CreateError("Email is required"));
        }

        var user = await _userManager.FindByEmailAsync(request.Email);

        // Mirror the web ForgotPassword flow: only send for active, email-confirmed
        // accounts, but never reveal that state to the caller (anti-enumeration).
        if (user is { IsDeleted: false } && await _userManager.IsEmailConfirmedAsync(user))
        {
            try
            {
                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                var callbackUrl = Url.Page(
                    "/Account/ResetPassword",
                    pageHandler: null,
                    values: new { area = "Identity", code },
                    protocol: Request.Scheme);

                await _emailService.SendEmailAsync(
                    user.Email!,
                    "Reset your FixIt password",
                    $"Reset your password by <a href='{HtmlEncoder.Default.Encode(callbackUrl!)}'>clicking here</a>.");

                _logger.LogInformation("Password reset email sent for user {UserId}", user.Id);
            }
            catch (Exception ex)
            {
                // Delivery failures must not leak account state or fail the request.
                _logger.LogError(ex, "Failed to send password reset email for user {UserId}", user.Id);
            }
        }
        else
        {
            _logger.LogInformation("Password reset requested for a non-existent, inactive, or unconfirmed email");
        }

        return Ok(ApiResponse<object>.CreateSuccess(
            new { message = "If an account exists for that email, a password reset link has been sent." },
            "Password reset requested"));
    }
}

/// <summary>
/// Request payload for <c>POST api/auth/forgot-password</c>.
/// </summary>
public class ForgotPasswordRequest
{
    public string? Email { get; set; }
}
