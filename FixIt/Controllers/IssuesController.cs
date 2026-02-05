using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using FixIt.Services.Contracts;
using FixIt.Models.Common;
using FixIt.ViewModels;
using FixIt.ViewModels.Issues;

namespace FixIt.Controllers;

/// <summary>
/// Issues controller - handles all issue-related operations
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class IssuesController : ControllerBase
{
    private readonly IIssueService _issueService;
    private readonly ITagService _tagService;
    private readonly ILogger<IssuesController> _logger;

    public IssuesController(
        IIssueService issueService,
        ITagService tagService,
        ILogger<IssuesController> logger)
    {
        _issueService = issueService;
        _tagService = tagService;
        _logger = logger;
    }

    /// <summary>
    /// Create a new issue
    /// </summary>
    /// <param name="request">Issue creation request</param>
    /// <returns>The created issue</returns>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<IssueDetailResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<IssueDetailResponse>>> CreateIssue([FromBody] CreateIssueRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(ApiResponse<object>.CreateError("User identity not found"));
            }

            // Parse tags from request
            IEnumerable<string>? tagNames = null;
            if (!string.IsNullOrEmpty(request.TagsJson))
            {
                try
                {
                    // Try to parse as comma-separated list
                    tagNames = request.TagsJson.Split(',')
                        .Select(t => t.Trim())
                        .Where(t => !string.IsNullOrEmpty(t))
                        .ToList();
                }
                catch
                {
                    _logger.LogWarning("Failed to parse tags from request: {TagsJson}", request.TagsJson);
                }
            }

            var reporter = new UserSummary
            {
                Id = userId,
                DisplayName = User.FindFirstValue(ClaimTypes.Name) ?? "Anonymous",
                AvatarUrl = null
            };

            var issue = await _issueService.CreateIssueAsync(
                request.Title,
                request.Description,
                request.Longitude,
                request.Latitude,
                request.CityId,
                reporter,
                tagNames
            );

            _logger.LogInformation("Issue created: {IssueId} by user {UserId}", issue.Id, userId);

            return CreatedAtAction(
                nameof(GetIssueById),
                new { id = issue.Id },
                ApiResponse<IssueDetailResponse>.CreateSuccess(
                    issue.ToDetailResponse(),
                    "Issue created successfully"
                )
            );
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ApiResponse<object>.CreateError(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating issue");
            return BadRequest(ApiResponse<object>.CreateError("Failed to create issue"));
        }
    }

    /// <summary>
    /// Get issue by ID
    /// </summary>
    /// <param name="id">Issue ID</param>
    /// <returns>Issue details</returns>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ApiResponse<IssueDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IssueDetailResponse>>> GetIssueById(string id)
    {
        try
        {
            var issue = await _issueService.GetIssueByIdAsync(id);
            if (issue == null)
            {
                return NotFound(ApiResponse<object>.CreateError("Issue not found"));
            }

            return Ok(ApiResponse<IssueDetailResponse>.CreateSuccess(issue.ToDetailResponse()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching issue {IssueId}", id);
            return BadRequest(ApiResponse<object>.CreateError("Failed to fetch issue"));
        }
    }

    /// <summary>
    /// Get issues by city
    /// </summary>
    /// <param name="cityId">City ID</param>
    /// <param name="status">Optional status filter</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Page size (default: 20, max: 100)</param>
    /// <returns>Paginated list of issues</returns>
    [HttpGet("city/{cityId}")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<IssueSummaryResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<IssueSummaryResponse>>>> GetIssuesByCity(
        string cityId,
        [FromQuery] int? status = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var statusEnum = status.HasValue && Enum.IsDefined(typeof(FixIt.Models.Enums.IssueStatus), status.Value)
                ? (FixIt.Models.Enums.IssueStatus)status.Value
                : (FixIt.Models.Enums.IssueStatus?)null;

            var result = await _issueService.GetIssuesByCityAsync(
                cityId,
                statusEnum,
                page,
                pageSize
            );

            return Ok(ApiResponse<PaginatedResponse<IssueSummaryResponse>>.CreateSuccess(
                result.ToPaginatedResponse(page, pageSize)
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching issues for city {CityId}", cityId);
            return BadRequest(ApiResponse<object>.CreateError("Failed to fetch issues"));
        }
    }

    /// <summary>
    /// DEBUG: Get raw issues data for city (unfiltered)
    /// </summary>
    [HttpGet("debug/city/{cityId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<object>>> DebugGetIssuesForCity(string cityId)
    {
        try
        {
            var result = await _issueService.GetIssuesByCityAsync(cityId, null, 1, 100);
            
            var debugData = new
            {
                cityId,
                totalFound = result.Total,
                issues = result.Items.Select(i => new
                {
                    id = i.Id,
                    title = i.Title,
                    cityId = i.CityId,
                    hasLocation = i.Location != null,
                    hasCoordinates = i.Location?.Coordinates != null,
                    latitude = i.Location?.Coordinates?.Latitude,
                    longitude = i.Location?.Coordinates?.Longitude,
                    status = i.Status.ToString(),
                    priority = i.Priority.ToString(),
                    createdAt = i.CreatedAt
                }).ToList()
            };

            return Ok(ApiResponse<object>.CreateSuccess(debugData));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Debug error for city {CityId}", cityId);
            return BadRequest(ApiResponse<object>.CreateError($"Debug error: {ex.Message}"));
        }
    }

    /// <summary>
    /// Search issues with advanced filtering
    /// </summary>
    /// <param name="cityId">City ID</param>
    /// <param name="request">Search request parameters</param>
    /// <returns>Paginated search results</returns>
    [HttpPost("city/{cityId}/search")]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<IssueSummaryResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<IssueSummaryResponse>>>> SearchIssues(
        string cityId,
        [FromBody] SearchIssuesRequest request)
    {
        try
        {
            var result = await _issueService.SearchIssuesAsync(
                cityId,
                request.SearchQuery,
                request.TagIds,
                request.Status,
                request.Priority,
                request.Page,
                request.PageSize
            );

            return Ok(ApiResponse<PaginatedResponse<IssueSummaryResponse>>.CreateSuccess(
                result.ToPaginatedResponse(request.Page, request.PageSize)
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error searching issues for city {CityId}", cityId);
            return BadRequest(ApiResponse<object>.CreateError("Failed to search issues"));
        }
    }

    /// <summary>
    /// Get user's issues
    /// </summary>
    /// <returns>Paginated list of user's issues</returns>
    [HttpGet("my-issues")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<PaginatedResponse<IssueSummaryResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<PaginatedResponse<IssueSummaryResponse>>>> GetUserIssues(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(ApiResponse<object>.CreateError("User identity not found"));
            }

            var result = await _issueService.GetUserIssuesAsync(userId, page, pageSize);

            return Ok(ApiResponse<PaginatedResponse<IssueSummaryResponse>>.CreateSuccess(
                result.ToPaginatedResponse(page, pageSize)
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user issues");
            return BadRequest(ApiResponse<object>.CreateError("Failed to fetch issues"));
        }
    }

    /// <summary>
    /// Update issue status
    /// </summary>
    /// <param name="id">Issue ID</param>
    /// <param name="request">Status update request</param>
    /// <returns>Updated issue</returns>
    [HttpPut("{id}/status")]
    [Authorize(Roles = "Moderator,Admin")]
    [ProducesResponseType(typeof(ApiResponse<IssueDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IssueDetailResponse>>> UpdateIssueStatus(
        string id,
        [FromBody] UpdateIssueStatusRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(ApiResponse<object>.CreateError("User identity not found"));
            }

            await _issueService.UpdateIssueStatusAsync(id, request.NewStatus, userId, request.Comment);

            var issue = await _issueService.GetIssueByIdAsync(id);
            if (issue == null)
            {
                return NotFound(ApiResponse<object>.CreateError("Issue not found"));
            }

            _logger.LogInformation("Issue {IssueId} status updated to {Status} by user {UserId}",
                id, request.NewStatus, userId);

            return Ok(ApiResponse<IssueDetailResponse>.CreateSuccess(
                issue.ToDetailResponse(),
                "Issue status updated successfully"
            ));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.CreateError(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating issue status {IssueId}", id);
            return BadRequest(ApiResponse<object>.CreateError("Failed to update issue status"));
        }
    }

    /// <summary>
    /// Update issue priority
    /// </summary>
    /// <param name="id">Issue ID</param>
    /// <param name="request">Priority update request</param>
    /// <returns>Updated issue</returns>
    [HttpPut("{id}/priority")]
    [Authorize(Roles = "Moderator,Admin")]
    [ProducesResponseType(typeof(ApiResponse<IssueDetailResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IssueDetailResponse>>> UpdateIssuePriority(
        string id,
        [FromBody] UpdateIssuePriorityRequest request)
    {
        try
        {
            await _issueService.UpdateIssuePriorityAsync(id, request.Priority);

            var issue = await _issueService.GetIssueByIdAsync(id);
            if (issue == null)
            {
                return NotFound(ApiResponse<object>.CreateError("Issue not found"));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            _logger.LogInformation("Issue {IssueId} priority updated to {Priority} by user {UserId}",
                id, request.Priority, userId);

            return Ok(ApiResponse<IssueDetailResponse>.CreateSuccess(
                issue.ToDetailResponse(),
                "Issue priority updated successfully"
            ));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.CreateError(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating issue priority {IssueId}", id);
            return BadRequest(ApiResponse<object>.CreateError("Failed to update issue priority"));
        }
    }

    /// <summary>
    /// Vote on an issue
    /// </summary>
    /// <param name="id">Issue ID</param>
    /// <param name="request">Vote request</param>
    /// <returns>Success response</returns>
    [HttpPost("{id}/vote")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> VoteOnIssue(
        string id,
        [FromBody] VoteRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(ApiResponse<object>.CreateError("User identity not found"));
            }

            await _issueService.AddVoteAsync(id, userId, request.VoteType);

            return Ok(ApiResponse<object>.CreateSuccess(
                new { message = "Vote recorded successfully" },
                "Vote recorded"
            ));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.CreateError(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error voting on issue {IssueId}", id);
            return BadRequest(ApiResponse<object>.CreateError("Failed to record vote"));
        }
    }

    /// <summary>
    /// Remove vote from an issue
    /// </summary>
    /// <param name="id">Issue ID</param>
    /// <returns>Success response</returns>
    [HttpDelete("{id}/vote")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ApiResponse<object>>> RemoveVote(string id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(ApiResponse<object>.CreateError("User identity not found"));
            }

            await _issueService.RemoveVoteAsync(id, userId);

            return Ok(ApiResponse<object>.CreateSuccess(
                new { message = "Vote removed successfully" },
                "Vote removed"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing vote from issue {IssueId}", id);
            return BadRequest(ApiResponse<object>.CreateError("Failed to remove vote"));
        }
    }

    /// <summary>
    /// Delete an issue (soft delete)
    /// </summary>
    /// <param name="id">Issue ID</param>
    /// <returns>Success response</returns>
    [HttpDelete("{id}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteIssue(string id)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(ApiResponse<object>.CreateError("User identity not found"));
            }

            var issue = await _issueService.GetIssueByIdAsync(id);
            if (issue == null)
            {
                return NotFound(ApiResponse<object>.CreateError("Issue not found"));
            }

            // Only allow deletion by issue reporter or admins
            if (issue.Reporter.Id != userId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }

            await _issueService.DeleteIssueAsync(id);

            _logger.LogInformation("Issue {IssueId} deleted by user {UserId}", id, userId);

            return Ok(ApiResponse<object>.CreateSuccess(
                new { message = "Issue deleted successfully" },
                "Issue deleted"
            ));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ApiResponse<object>.CreateError(ex.Message));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting issue {IssueId}", id);
            return BadRequest(ApiResponse<object>.CreateError("Failed to delete issue"));
        }
    }

    /// <summary>
    /// Get issues by city
    /// </summary>
    /// <param name="cityId">The city ID</param>
    /// <param name="page">Page number (default: 1)</param>
    /// <param name="pageSize">Items per page (default: 20, max: 100)</param>
    /// <returns>Paginated list of issues for the city</returns>
    [HttpGet("by-city/{cityId}")]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ApiResponse<object>>> GetIssuesByCity(
        string cityId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            // Validate parameters
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 1;
            if (pageSize > 100) pageSize = 100;

            var issues = await _issueService.GetIssuesByCityAsync(cityId);
            var total = await _issueService.GetIssueCountByCityAsync(cityId);

            var response = new
            {
                issues = issues,
                pagination = new
                {
                    currentPage = page,
                    pageSize = pageSize,
                    totalItems = total,
                    totalPages = (int)Math.Ceiling((double)total / pageSize)
                }
            };

            return Ok(ApiResponse<object>.CreateSuccess(response, "Issues retrieved successfully"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting issues for city {CityId}", cityId);
            return BadRequest(ApiResponse<object>.CreateError("Failed to retrieve issues"));
        }
    }
}

