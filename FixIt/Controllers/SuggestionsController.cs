using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using FixIt.Services.AI;
using FixIt.Models.Users;
using FixIt.ViewModels;

namespace FixIt.Controllers;

/// <summary>
/// API controller for admin suggestions
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin,Moderator")]
public class SuggestionsController : ControllerBase
{
    private readonly IAdminSuggestionsService _suggestionsService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<SuggestionsController> _logger;

    public SuggestionsController(
        IAdminSuggestionsService suggestionsService,
        UserManager<ApplicationUser> userManager,
        ILogger<SuggestionsController> logger)
    {
        _suggestionsService = suggestionsService;
        _userManager = userManager;
        _logger = logger;
    }

    /// <summary>
    /// Get pending suggestions for dashboard
    /// </summary>
    [HttpGet("pending")]
    public async Task<ActionResult<List<AdminSuggestionViewModel>>> GetPendingSuggestions([FromQuery] int limit = 10)
    {
        try
        {
            var suggestions = await _suggestionsService.GetPendingSuggestionsAsync(limit);
            var viewModels = suggestions.Select(AdminSuggestionViewModel.FromModel).ToList();
            return Ok(viewModels);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving pending suggestions: {ex.Message}");
            return StatusCode(500, new { error = "Failed to retrieve suggestions" });
        }
    }

    /// <summary>
    /// Get suggestions for a specific entity (report, issue, user)
    /// </summary>
    [HttpGet("entity/{entityId}")]
    public async Task<ActionResult<List<AdminSuggestionViewModel>>> GetSuggestionsForEntity(
        string entityId,
        [FromQuery] string entityType = "")
    {
        if (string.IsNullOrEmpty(entityId))
            return BadRequest(new { error = "Entity ID is required" });

        try
        {
            var suggestions = await _suggestionsService.GetSuggestionsForEntityAsync(
                entityId, 
                entityType);

            var viewModels = suggestions.Select(AdminSuggestionViewModel.FromModel).ToList();
            return Ok(viewModels);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving suggestions for entity {entityId}: {ex.Message}");
            return StatusCode(500, new { error = "Failed to retrieve suggestions" });
        }
    }

    /// <summary>
    /// Generate suggestion for a report
    /// </summary>
    [HttpPost("report/{reportId}")]
    public async Task<ActionResult<AdminSuggestionViewModel>> GenerateReportSuggestion(string reportId)
    {
        if (string.IsNullOrEmpty(reportId))
            return BadRequest(new { error = "Report ID is required" });

        try
        {
            var suggestion = await _suggestionsService.SuggestReportActionAsync(reportId);
            if (suggestion == null)
                return NoContent();

            return Ok(AdminSuggestionViewModel.FromModel(suggestion));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error generating report suggestion: {ex.Message}");
            return StatusCode(500, new { error = "Failed to generate suggestion" });
        }
    }

    /// <summary>
    /// Generate suggestions for an issue
    /// </summary>
    [HttpPost("issue/{issueId}")]
    public async Task<ActionResult<List<AdminSuggestionViewModel>>> GenerateIssueSuggestions(string issueId)
    {
        if (string.IsNullOrEmpty(issueId))
            return BadRequest(new { error = "Issue ID is required" });

        try
        {
            var suggestions = await _suggestionsService.SuggestIssueActionsAsync(issueId);
            var viewModels = suggestions.Select(AdminSuggestionViewModel.FromModel).ToList();
            return Ok(viewModels);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error generating issue suggestions: {ex.Message}");
            return StatusCode(500, new { error = "Failed to generate suggestions" });
        }
    }

    /// <summary>
    /// Generate suggestion for user moderation
    /// </summary>
    [HttpPost("user/{userId}")]
    public async Task<ActionResult<AdminSuggestionViewModel>> GenerateUserModerationSuggestion(string userId)
    {
        if (string.IsNullOrEmpty(userId))
            return BadRequest(new { error = "User ID is required" });

        try
        {
            var suggestion = await _suggestionsService.SuggestUserModerationAsync(userId);
            if (suggestion == null)
                return NoContent();

            return Ok(AdminSuggestionViewModel.FromModel(suggestion));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error generating user moderation suggestion: {ex.Message}");
            return StatusCode(500, new { error = "Failed to generate suggestion" });
        }
    }

    /// <summary>
    /// Mark suggestion as acted upon
    /// </summary>
    [HttpPost("{suggestionId}/act")]
    public async Task<IActionResult> ActOnSuggestion(
        string suggestionId,
        [FromBody] ActOnSuggestionRequest request)
    {
        if (string.IsNullOrEmpty(suggestionId) || request == null)
            return BadRequest(new { error = "Suggestion ID and action are required" });

        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            await _suggestionsService.MarkAsActedAsync(suggestionId, request.ActionTaken, user.Id.ToString());
            return Ok(new { message = "Suggestion marked as acted upon" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error marking suggestion as acted: {ex.Message}");
            return StatusCode(500, new { error = "Failed to mark suggestion as acted" });
        }
    }

    /// <summary>
    /// Invalidate a suggestion
    /// </summary>
    [HttpPost("{suggestionId}/invalidate")]
    public async Task<IActionResult> InvalidateSuggestion(string suggestionId)
    {
        if (string.IsNullOrEmpty(suggestionId))
            return BadRequest(new { error = "Suggestion ID is required" });

        try
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Unauthorized();

            await _suggestionsService.InvalidateSuggestionAsync(suggestionId, user.Id.ToString());
            return Ok(new { message = "Suggestion invalidated" });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error invalidating suggestion: {ex.Message}");
            return StatusCode(500, new { error = "Failed to invalidate suggestion" });
        }
    }

    /// <summary>
    /// Get a specific suggestion
    /// </summary>
    [HttpGet("{suggestionId}")]
    public async Task<ActionResult<AdminSuggestionViewModel>> GetSuggestion(string suggestionId)
    {
        if (string.IsNullOrEmpty(suggestionId))
            return BadRequest(new { error = "Suggestion ID is required" });

        try
        {
            var suggestion = await _suggestionsService.GetSuggestionAsync(suggestionId);
            if (suggestion == null)
                return NotFound();

            return Ok(AdminSuggestionViewModel.FromModel(suggestion));
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error retrieving suggestion: {ex.Message}");
            return StatusCode(500, new { error = "Failed to retrieve suggestion" });
        }
    }
}

public class ActOnSuggestionRequest
{
    public string ActionTaken { get; set; } = string.Empty;
}
