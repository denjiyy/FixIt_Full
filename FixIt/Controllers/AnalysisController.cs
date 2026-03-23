using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FixIt.Services.AI;
using FixIt.Models.AI;

namespace FixIt.Controllers;

/// <summary>
/// API controller for AI analysis operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AnalysisController : ControllerBase
{
    private readonly IIssueAnalysisService _analysisService;
    private readonly ILogger<AnalysisController> _logger;

    public AnalysisController(
        IIssueAnalysisService analysisService,
        ILogger<AnalysisController> logger)
    {
        _analysisService = analysisService;
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
            _logger.LogInformation($"Requesting AI analysis for issue {issueId}");
            
            var analysis = await _analysisService.AnalyzeIssueAsync(issueId);
            
            return Ok(new AnalysisResponse
            {
                Success = true,
                Analysis = analysis,
                Message = "Analysis completed successfully"
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning($"Issue not found: {issueId}");
            return NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError($"Analysis failed for issue {issueId}: {ex.Message}");
            return StatusCode(500, new { error = "Analysis failed", details = ex.Message });
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
            _logger.LogError($"Failed to retrieve analysis for issue {issueId}: {ex.Message}");
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
}

public class TagSuggestionRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
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
