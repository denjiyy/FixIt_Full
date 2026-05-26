using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using FixIt.Services.AI;
using FixIt.Models.AI;
using FixIt.Services.Constants;

namespace FixIt.Controllers;

/// <summary>
/// API controller for AI analysis operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[EnableRateLimiting(RateLimitPolicyNames.Reporting)]
public class AnalysisController : ControllerBase
{
    private readonly IIssueAnalysisService _analysisService;
    private readonly ICivicAiService _civicAiService;
    private readonly ILogger<AnalysisController> _logger;

    public AnalysisController(
        IIssueAnalysisService analysisService,
        ICivicAiService civicAiService,
        ILogger<AnalysisController> logger)
    {
        _analysisService = analysisService;
        _civicAiService = civicAiService;
        _logger = logger;
    }

    /// <summary>
    /// Trigger AI analysis for an issue
    /// </summary>
    [HttpPost("analyze/{issueId}")]
    [AllowAnonymous]
    public async Task<ActionResult<AnalysisResponse>> AnalyzeIssue(string issueId)
    {
        if (string.IsNullOrEmpty(issueId))
        {
            return BadRequest(new { error = "Issue ID is required" });
        }

        try
        {
            _logger.LogInformation("Requesting AI analysis for issue {IssueId}", issueId);
            
            var analysis = await _analysisService.AnalyzeIssueAsync(issueId);
            
            return Ok(new AnalysisResponse
            {
                Success = true,
                Analysis = analysis,
                Message = "Analysis completed successfully"
            });
        }
        catch (InvalidOperationException)
        {
            _logger.LogWarning("Issue not found for analysis {IssueId}", issueId);
            return NotFound(new { error = "Issue not found" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analysis failed for issue {IssueId}", issueId);
            return StatusCode(500, new { error = "Analysis failed" });
        }
    }

    /// <summary>
    /// Suggest issue category / severity / department from draft data.
    /// </summary>
    [HttpPost("issue-draft-suggestions")]
    [Authorize]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<ActionResult<IssueDraftSuggestionResult>> SuggestIssueDraft([FromForm] IssueDraftSuggestionRequest request, CancellationToken cancellationToken)
    {
        if (request == null || (string.IsNullOrWhiteSpace(request.Title) && string.IsNullOrWhiteSpace(request.Description)))
        {
            return BadRequest(new { error = "Title or description is required." });
        }

        try
        {
            byte[]? imageBytes = null;
            string? imageMime = null;

            if (request.Image is { Length: > 0 } && request.Image.Length <= 2 * 1024 * 1024 &&
                request.Image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                await using var stream = request.Image.OpenReadStream();
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory, cancellationToken);
                imageBytes = memory.ToArray();
                imageMime = request.Image.ContentType;
            }

            var result = await _civicAiService.SuggestIssueDraftAsync(new IssueDraftSuggestionInput
            {
                Title = request.Title?.Trim() ?? string.Empty,
                Description = request.Description?.Trim() ?? string.Empty,
                CityId = string.IsNullOrWhiteSpace(request.CityId) ? null : request.CityId.Trim(),
                ImageBytes = imageBytes,
                ImageMimeType = imageMime
            }, cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to provide issue draft suggestions");
            return StatusCode(500, new { error = "Failed to generate draft suggestions." });
        }
    }

    /// <summary>
    /// Get cached analysis for an issue
    /// </summary>
    [HttpGet("analyze/{issueId}")]
    [AllowAnonymous]
    public async Task<ActionResult<AnalysisResponse>> GetAnalysis(string issueId)
    {
        if (string.IsNullOrEmpty(issueId))
        {
            return BadRequest(new { error = "Issue ID is required" });
        }

        try
        {
            var analysis = await _analysisService.GetAnalysisAsync(issueId);
            
            if (analysis == null)
            {
                return Ok(new AnalysisResponse
                {
                    Success = false,
                    Analysis = null,
                    Message = "No analysis available. Analysis can be triggered via POST endpoint."
                });
            }

            return Ok(new AnalysisResponse
            {
                Success = true,
                Analysis = analysis,
                Message = "Analysis retrieved successfully"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve analysis for issue {IssueId}", issueId);
            return StatusCode(500, new { error = "Failed to retrieve analysis" });
        }
    }

    /// <summary>
    /// Suggest tags for an issue draft (title + description)
    /// </summary>
    [HttpPost("suggest-tags")]
    [AllowAnonymous]
    public async Task<ActionResult<IEnumerable<string>>> SuggestTags([FromBody] TagSuggestionRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Title) && string.IsNullOrWhiteSpace(request.Description))
        {
            return BadRequest(new { error = "Title or description is required to suggest tags" });
        }

        try
        {
            var tags = await _analysisService.SuggestTagsAsync(request.Title ?? string.Empty, request.Description ?? string.Empty);
            return Ok(tags);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to suggest tags");
            return StatusCode(500, new { error = "Failed to suggest tags" });
        }
    }

    /// <summary>
    /// Translate natural-language issue query into structured filters.
    /// </summary>
    [HttpPost("issue-search/translate")]
    [AllowAnonymous]
    public async Task<ActionResult<IssueFilterTranslationResult>> TranslateIssueSearch([FromBody] IssueSearchTranslationRequest request, CancellationToken cancellationToken)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Query))
        {
            return BadRequest(new { error = "Query is required." });
        }

        try
        {
            var result = await _civicAiService.TranslateIssueFilterAsync(new IssueFilterTranslationInput
            {
                Query = request.Query.Trim()
            }, cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to translate issue search query");
            return StatusCode(500, new { error = "Failed to translate search query." });
        }
    }
}

public class TagSuggestionRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
}

public sealed class IssueDraftSuggestionRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? CityId { get; set; }
    public IFormFile? Image { get; set; }
}

public sealed class IssueSearchTranslationRequest
{
    public string? Query { get; set; }
}

/// <summary>
/// Response model for analysis API
/// </summary>
public class AnalysisResponse
{
    public bool Success { get; set; }
    public IssueAnalysis? Analysis { get; set; }
    public string Message { get; set; } = string.Empty;
}
