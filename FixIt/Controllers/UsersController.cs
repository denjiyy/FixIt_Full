using FixIt.Extensions;
using FixIt.Models.Users;
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
    private readonly ILogger<UsersController> _logger;

    public UsersController(UserManager<ApplicationUser> userManager, ILogger<UsersController> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    [HttpPost("profile-visibility")]
    [ApiAuthorize]
    [ValidateAntiForgeryToken]
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
