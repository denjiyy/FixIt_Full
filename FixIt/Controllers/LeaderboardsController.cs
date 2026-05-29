using FixIt.Extensions;
using FixIt.Models.Gamification;
using FixIt.Services.Gamification;
using FixIt.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FixIt.Controllers;

/// <summary>
/// Read-only leaderboard endpoint. Public data, consumed by the mobile app at
/// <c>api/leaderboards?period=weekly|monthly|alltime</c> and usable from the web.
/// </summary>
[ApiController]
[Route("api/leaderboards")]
[Produces("application/json")]
public class LeaderboardsController : ControllerBase
{
    private readonly IReputationService _reputationService;
    private readonly ILogger<LeaderboardsController> _logger;

    public LeaderboardsController(IReputationService reputationService, ILogger<LeaderboardsController> logger)
    {
        _reputationService = reputationService;
        _logger = logger;
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<LeaderboardEntry>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<List<LeaderboardEntry>>>> GetLeaderboard(
        [FromQuery] string period = "weekly",
        [FromQuery] int take = 50)
    {
        var normalizedPeriod = (period ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "monthly" => LeaderboardPeriod.Monthly,
            "alltime" or "all-time" or "all" => LeaderboardPeriod.AllTime,
            _ => LeaderboardPeriod.Weekly
        };

        take = Math.Clamp(take, 1, 200);

        try
        {
            var entries = await _reputationService.GetLeaderboardAsync(normalizedPeriod, take);
            return Ok(ApiResponse<List<LeaderboardEntry>>.CreateSuccess(entries, $"Leaderboard ({normalizedPeriod})"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching leaderboard for period {Period}", normalizedPeriod);
            return BadRequest(ApiResponse<object>.CreateError("Failed to fetch leaderboard"));
        }
    }
}
