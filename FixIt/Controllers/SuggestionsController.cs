using FixIt.Extensions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Text.Json;
using FixIt.Services.AI;
using FixIt.Models.Users;
using FixIt.ViewModels;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Moderation;
using FixIt.Services.Contracts;
using FixIt.Services.Constants;

namespace FixIt.Controllers;

/// <summary>
/// API controller for admin suggestions
/// </summary>
[ApiController]
[Route("api/[controller]")]
[ApiAuthorize(PolicyNames.AdminArea)]
[EnableRateLimiting(RateLimitPolicyNames.Reporting)]
public class SuggestionsController : ControllerBase
{
    private readonly IAdminSuggestionsService _suggestionsService;
    private readonly IIssueService _issueService;
    private readonly ICommentService _commentService;
    private readonly IRepository<ContentReport> _reportRepository;
    private readonly ICivicAiService _civicAiService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<SuggestionsController> _logger;

    public SuggestionsController(
        IAdminSuggestionsService suggestionsService,
        IIssueService issueService,
        ICommentService commentService,
        IRepository<ContentReport> reportRepository,
        ICivicAiService civicAiService,
        UserManager<ApplicationUser> userManager,
        ILogger<SuggestionsController> logger)
    {
        _suggestionsService = suggestionsService;
        _issueService = issueService;
        _commentService = commentService;
        _reportRepository = reportRepository;
        _civicAiService = civicAiService;
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
            _logger.LogError(ex, "Error retrieving pending suggestions");
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
            _logger.LogError(ex, "Error retrieving suggestions for entity {EntityId}", entityId);
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
            _logger.LogError(ex, "Error generating report suggestion");
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
            _logger.LogError(ex, "Error generating issue suggestions");
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
            _logger.LogError(ex, "Error generating user moderation suggestion");
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
            _logger.LogError(ex, "Error marking suggestion as acted");
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
            _logger.LogError(ex, "Error invalidating suggestion");
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
            _logger.LogError(ex, "Error retrieving suggestion");
            return StatusCode(500, new { error = "Failed to retrieve suggestion" });
        }
    }

    [HttpPost("issues/{issueId}/summary")]
    public async Task<ActionResult<object>> SummarizeIssue(string issueId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(issueId))
        {
            return BadRequest(new { error = "Issue ID is required" });
        }

        try
        {
            var issue = await _issueService.GetIssueByIdAsync(issueId);
            if (issue == null)
            {
                return NotFound(new { error = "Issue not found" });
            }

            var comments = await _commentService.GetCommentsForIssueAsync(issueId);
            var result = await _civicAiService.SummarizeIssueThreadAsync(new IssueThreadSummaryInput
            {
                IssueId = issueId,
                Title = issue.Title,
                Description = issue.Description,
                Comments = comments
                    .Where(c => !c.IsDeleted)
                    .Select(c => c.Text)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Take(12)
                    .ToList()
            }, cancellationToken);

            return Ok(new
            {
                content = result.Content,
                aiGenerated = result.AiGenerated,
                fallbackUsed = result.FallbackUsed
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating issue summary for {IssueId}", issueId);
            return StatusCode(500, new { error = "Failed to summarize issue" });
        }
    }

    [HttpPost("issues/{issueId}/summary/stream")]
    public async Task StreamIssueSummary(string issueId, CancellationToken cancellationToken)
    {
        Response.ContentType = "application/x-ndjson";

        if (string.IsNullOrWhiteSpace(issueId))
        {
            await WriteNdjsonEventAsync(new AiStreamEvent { Type = "error", Message = "Issue ID is required." }, cancellationToken);
            return;
        }

        try
        {
            var issue = await _issueService.GetIssueByIdAsync(issueId);
            if (issue == null)
            {
                await WriteNdjsonEventAsync(new AiStreamEvent { Type = "error", Message = "Issue not found." }, cancellationToken);
                return;
            }

            var comments = await _commentService.GetCommentsForIssueAsync(issueId);
            await foreach (var aiEvent in _civicAiService.StreamIssueThreadSummaryAsync(new IssueThreadSummaryInput
                           {
                               IssueId = issueId,
                               Title = issue.Title,
                               Description = issue.Description,
                               Comments = comments
                                   .Where(c => !c.IsDeleted)
                                   .Select(c => c.Text)
                                   .Where(t => !string.IsNullOrWhiteSpace(t))
                                   .Take(12)
                                   .ToList()
                           }, cancellationToken))
            {
                await WriteNdjsonEventAsync(aiEvent, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming issue summary for {IssueId}", issueId);
            await WriteNdjsonEventAsync(new AiStreamEvent { Type = "error", Message = "Failed to stream issue summary." }, cancellationToken);
        }
    }

    [HttpPost("reports/{reportId}/summary")]
    public async Task<ActionResult<object>> SummarizeReport(string reportId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reportId))
        {
            return BadRequest(new { error = "Report ID is required" });
        }

        try
        {
            var report = await _reportRepository.GetByIdAsync(reportId);
            if (report == null)
            {
                return NotFound(new { error = "Report not found" });
            }

            var reportInput = await BuildReportSummaryInputAsync(report, cancellationToken);
            var result = await _civicAiService.SummarizeReportAsync(reportInput, cancellationToken);
            return Ok(new
            {
                content = result.Content,
                aiGenerated = result.AiGenerated,
                fallbackUsed = result.FallbackUsed
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating report summary for {ReportId}", reportId);
            return StatusCode(500, new { error = "Failed to summarize report" });
        }
    }

    [HttpPost("reports/{reportId}/summary/stream")]
    public async Task StreamReportSummary(string reportId, CancellationToken cancellationToken)
    {
        Response.ContentType = "application/x-ndjson";

        if (string.IsNullOrWhiteSpace(reportId))
        {
            await WriteNdjsonEventAsync(new AiStreamEvent { Type = "error", Message = "Report ID is required." }, cancellationToken);
            return;
        }

        try
        {
            var report = await _reportRepository.GetByIdAsync(reportId);
            if (report == null)
            {
                await WriteNdjsonEventAsync(new AiStreamEvent { Type = "error", Message = "Report not found." }, cancellationToken);
                return;
            }

            var reportInput = await BuildReportSummaryInputAsync(report, cancellationToken);
            await foreach (var aiEvent in _civicAiService.StreamReportSummaryAsync(reportInput, cancellationToken))
            {
                await WriteNdjsonEventAsync(aiEvent, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming report summary for {ReportId}", reportId);
            await WriteNdjsonEventAsync(new AiStreamEvent { Type = "error", Message = "Failed to stream report summary." }, cancellationToken);
        }
    }

    private async Task<ReportSummaryInput> BuildReportSummaryInputAsync(ContentReport report, CancellationToken cancellationToken)
    {
        var reportInput = new ReportSummaryInput
        {
            ReportId = report.Id,
            Reason = report.Reason.ToString(),
            Details = report.Details,
            TargetId = report.TargetId,
            TargetType = report.TargetType.ToString()
        };

        if (report.TargetType == FixIt.Models.Enums.ModerationTargetType.Issue && !string.IsNullOrWhiteSpace(report.TargetId))
        {
            var issue = await _issueService.GetIssueByIdAsync(report.TargetId);
            if (issue != null)
            {
                var comments = await _commentService.GetCommentsForIssueAsync(report.TargetId);
                reportInput.IssueTitle = issue.Title;
                reportInput.IssueDescription = issue.Description;
                reportInput.IssueComments = comments
                    .Where(c => !c.IsDeleted)
                    .Select(c => c.Text)
                    .Where(t => !string.IsNullOrWhiteSpace(t))
                    .Take(12)
                    .ToList();
            }
        }

        return reportInput;
    }

    private async Task WriteNdjsonEventAsync(AiStreamEvent aiEvent, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(aiEvent);
        await Response.WriteAsync(json + "\n", cancellationToken);
        await Response.Body.FlushAsync(cancellationToken);
    }
}

public class ActOnSuggestionRequest
{
    public string ActionTaken { get; set; } = string.Empty;
}
