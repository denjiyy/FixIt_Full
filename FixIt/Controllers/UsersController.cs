using FixIt.Extensions;
using FixIt.Models.Users;
using FixIt.Services.Gamification;
using FixIt.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace FixIt.Controllers;

[ApiController]
[Route("api/users")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IReputationService _reputationService;
    private readonly ILogger<UsersController> _logger;

    public UsersController(
        UserManager<ApplicationUser> userManager,
        IReputationService reputationService,
        ILogger<UsersController> logger)
    {
        _userManager = userManager;
        _reputationService = reputationService;
        _logger = logger;
    }

    /// <summary>
    /// Public profile for a user (mobile: GET api/users/{id}/profile).
    /// Honors the user's ProfileVisibility setting.
    /// </summary>
    [HttpGet("{id}/profile")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> GetPublicProfile(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user == null || user.IsDeleted)
        {
            return NotFound(ApiResponse<object>.CreateError("User not found"));
        }

        if (!string.Equals(user.ProfileVisibility, "public", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound(ApiResponse<object>.CreateError("This profile is private"));
        }

        var reputation = await _reputationService.GetUserReputationAsync(id);

        return Ok(ApiResponse<object>.CreateSuccess(new
        {
            id = user.Id.ToString(),
            displayName = user.DisplayName,
            trustLevel = reputation?.TrustLevel ?? user.TrustLevel,
            reputationPoints = reputation?.TotalPoints ?? user.ReputationScore,
            issuesReported = reputation?.IssuesReported ?? 0,
            issuesResolved = reputation?.IssuesResolved ?? 0,
            commentsPosted = reputation?.CommentsPosted ?? 0
        }, "Profile retrieved"));
    }

    /// <summary>
    /// Gets the current user's email notification preferences.
    /// </summary>
    [HttpGet("email-preferences")]
    [ApiAuthorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> GetEmailPreferences()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(ApiResponse<object>.CreateError("User identity not found"));
        }

        return Ok(ApiResponse<object>.CreateSuccess(BuildEmailPreferences(user), "Email preferences retrieved"));
    }

    /// <summary>
    /// Updates the current user's email notification preferences.
    /// </summary>
    [HttpPost("email-preferences")]
    [ApiAuthorize]
    [ConditionalAntiforgery]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateEmailPreferences([FromBody] EmailPreferencesRequest request)
    {
        if (request == null)
        {
            return BadRequest(ApiResponse<object>.CreateError("Request payload is required."));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(ApiResponse<object>.CreateError("User identity not found"));
        }

        user.EmailNotificationsEnabled = request.Enabled;
        user.ReceiveHealthReports = request.WeeklyReports;
        user.ReceiveHazardAlerts = request.SafetyAlerts;
        user.ReceiveWeeklyReminders = request.Reminders;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed to update email preferences for user {UserId}", user.Id);
            return BadRequest(ApiResponse<object>.CreateError("Failed to update email preferences."));
        }

        return Ok(ApiResponse<object>.CreateSuccess(BuildEmailPreferences(user), "Email preferences updated"));
    }

    /// <summary>
    /// Gets the current user's preferred city.
    /// </summary>
    [HttpGet("city-preference")]
    [ApiAuthorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> GetCityPreference()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(ApiResponse<object>.CreateError("User identity not found"));
        }

        return Ok(ApiResponse<object>.CreateSuccess(new { cityId = user.PreferredCityId }, "City preference retrieved"));
    }

    /// <summary>
    /// Sets the current user's preferred city.
    /// </summary>
    [HttpPost("city-preference")]
    [ApiAuthorize]
    [ConditionalAntiforgery]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> SetCityPreference([FromBody] CityPreferenceRequest request)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(ApiResponse<object>.CreateError("User identity not found"));
        }

        user.PreferredCityId = string.IsNullOrWhiteSpace(request?.CityId) ? null : request.CityId.Trim();
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed to update city preference for user {UserId}", user.Id);
            return BadRequest(ApiResponse<object>.CreateError("Failed to update city preference."));
        }

        return Ok(ApiResponse<object>.CreateSuccess(new { cityId = user.PreferredCityId }, "City preference updated"));
    }

    private static object BuildEmailPreferences(ApplicationUser user) => new
    {
        enabled = user.EmailNotificationsEnabled,
        weeklyReports = user.ReceiveHealthReports,
        safetyAlerts = user.ReceiveHazardAlerts,
        reminders = user.ReceiveWeeklyReminders,
        cityScope = user.PreferredCityId
    };

    [HttpPost("profile-visibility")]
    [ApiAuthorize]
    [ConditionalAntiforgery]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> UpdateProfileVisibility([FromBody] UpdateProfileVisibilityRequest request)
    {
        var normalizedVisibility = NormalizeProfileVisibility(request?.Visibility);
        if (normalizedVisibility == null)
        {
            return BadRequest(ApiResponse<object>.CreateError("Profile visibility must be either public or private."));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized(ApiResponse<object>.CreateError("User identity not found"));
        }

        user.ProfileVisibility = normalizedVisibility;
        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            _logger.LogWarning("Failed to update profile visibility for user {UserId}", user.Id);
            return BadRequest(ApiResponse<object>.CreateError("Failed to update profile visibility."));
        }

        return Ok(ApiResponse<object>.CreateSuccess(new
        {
            visibility = user.ProfileVisibility
        }, "Profile visibility updated"));
    }

    private static string? NormalizeProfileVisibility(string? visibility)
    {
        if (string.IsNullOrWhiteSpace(visibility))
        {
            return null;
        }

        return visibility.Trim().ToLowerInvariant() switch
        {
            "public" => "public",
            "private" => "private",
            _ => null
        };
    }
}

public class UpdateProfileVisibilityRequest
{
    public string? Visibility { get; set; }
}

public class EmailPreferencesRequest
{
    public bool Enabled { get; set; } = true;
    public bool WeeklyReports { get; set; } = true;
    public bool SafetyAlerts { get; set; } = true;
    public bool Reminders { get; set; } = true;
    public string? CityScope { get; set; }
}

public class CityPreferenceRequest
{
    public string? CityId { get; set; }
}
