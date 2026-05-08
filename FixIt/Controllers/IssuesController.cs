using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using FixIt.Services.Contracts;
using FixIt.Services.Constants;
using FixIt.Models.Common;
using FixIt.Models.Users;
using FixIt.ViewModels;
using FixIt.ViewModels.Issues;
using FixIt.Services.AI;
using FixIt.Data.Repository.Contracts;
using Microsoft.AspNetCore.Identity;

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
    private readonly IIssueAnalysisService _analysisService;
    private readonly IRepository<ApplicationUser> _userRepo;
    private readonly ILogger<IssuesController> _logger;
    private readonly UserManager<ApplicationUser> _userManager;

    public IssuesController(
        IIssueService issueService,
        ITagService tagService,
        IIssueAnalysisService analysisService,
        IRepository<ApplicationUser> userRepo,
        ILogger<IssuesController> logger,
        UserManager<ApplicationUser> userManager)
    {
        _issueService = issueService;
        _tagService = tagService;
        _analysisService = analysisService;
        _userRepo = userRepo;
        _logger = logger;
        _userManager = userManager;
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
                return Unauthorized(ApiResponse<object>.CreateError(HttpErrorMessages.UserIdentityNotFound));
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

            // Check if anonymous posting is enabled when requesting anonymous post
            if (request.IsAnonymous)
            {
                var user = await _userRepo.GetByIdAsync(userId);
                if (user == null || !user.AnonymousReportingEnabled)
                    return BadRequest(ApiResponse<object>.CreateError("Anonymous posting is not enabled for your account. Please enable it in privacy settings first."));
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
                tagNames,
                request.IsAnonymous,
                request.Priority,
                request.Category,
                request.Department
            );

            _logger.LogInformation(
                request.IsAnonymous 
                    ? "Anonymous issue created: {IssueId}" 
                    : "Issue created: {IssueId} by user {UserId}", 
                issue.Id, userId);

            return CreatedAtAction(
                nameof(GetIssueById),
                new { id = issue.Id },
                ApiResponse<IssueDetailResponse>.CreateSuccess(
                    issue.ToDetailResponse(),
                    "Issue created successfully"
                )
            );
        }
        catch (ArgumentException)
        {
            return BadRequest(ApiResponse<object>.CreateError("Request could not be processed"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating issue");
            return BadRequest(ApiResponse<object>.CreateError(HttpErrorMessages.FailedToCreateIssue));
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
                return NotFound(ApiResponse<object>.CreateError(HttpErrorMessages.IssueNotFound));
            }

            return Ok(ApiResponse<IssueDetailResponse>.CreateSuccess(issue.ToDetailResponse()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching issue {IssueId}", id);
            return BadRequest(ApiResponse<object>.CreateError(HttpErrorMessages.FailedToFetchIssue));
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
            return BadRequest(ApiResponse<object>.CreateError(HttpErrorMessages.FailedToFetchIssues));
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
            return BadRequest(ApiResponse<object>.CreateError(HttpErrorMessages.FailedToSearchIssues));
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
                return Unauthorized(ApiResponse<object>.CreateError(HttpErrorMessages.UserIdentityNotFound));
            }

            var result = await _issueService.GetUserIssuesAsync(userId, page, pageSize);

            return Ok(ApiResponse<PaginatedResponse<IssueSummaryResponse>>.CreateSuccess(
                result.ToPaginatedResponse(page, pageSize)
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching user issues");
            return BadRequest(ApiResponse<object>.CreateError(HttpErrorMessages.FailedToFetchIssues));
        }
    }

    /// <summary>
    /// Update issue status
    /// </summary>
    /// <param name="id">Issue ID</param>
    /// <param name="request">Status update request</param>
    /// <returns>Updated issue</returns>
    [HttpPut("{id}/status")]
    [Authorize(Roles = RoleNames.ModeratorOrAdmin)]
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
                return Unauthorized(ApiResponse<object>.CreateError(HttpErrorMessages.UserIdentityNotFound));
            }

            await _issueService.UpdateIssueStatusAsync(id, request.NewStatus, userId, request.Comment);

            var issue = await _issueService.GetIssueByIdAsync(id);
            if (issue == null)
            {
                return NotFound(ApiResponse<object>.CreateError(HttpErrorMessages.IssueNotFound));
            }

            _logger.LogInformation("Issue {IssueId} status updated to {Status} by user {UserId}",
                id, request.NewStatus, userId);

            return Ok(ApiResponse<IssueDetailResponse>.CreateSuccess(
                issue.ToDetailResponse(),
                "Issue status updated successfully"
            ));
        }
        catch (InvalidOperationException)
        {
            return BadRequest(ApiResponse<object>.CreateError("Request could not be processed"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating issue status {IssueId}", id);
            return BadRequest(ApiResponse<object>.CreateError(HttpErrorMessages.FailedToUpdateIssueStatus));
        }
    }

    /// <summary>
    /// Update issue priority
    /// </summary>
    /// <param name="id">Issue ID</param>
    /// <param name="request">Priority update request</param>
    /// <returns>Updated issue</returns>
    [HttpPut("{id}/priority")]
    [Authorize(Roles = RoleNames.ModeratorOrAdmin)]
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
                return NotFound(ApiResponse<object>.CreateError(HttpErrorMessages.IssueNotFound));
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            _logger.LogInformation("Issue {IssueId} priority updated to {Priority} by user {UserId}",
                id, request.Priority, userId);

            return Ok(ApiResponse<IssueDetailResponse>.CreateSuccess(
                issue.ToDetailResponse(),
                "Issue priority updated successfully"
            ));
        }
        catch (InvalidOperationException)
        {
            return BadRequest(ApiResponse<object>.CreateError("Request could not be processed"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating issue priority {IssueId}", id);
            return BadRequest(ApiResponse<object>.CreateError(HttpErrorMessages.FailedToUpdateIssuePriority));
        }
    }

    /// <summary>
    /// Vote on an issue
    /// </summary>
    /// <param name="id">Issue ID</param>
    /// <param name="request">Vote request</param>
    /// <returns>Success response with updated vote counts</returns>
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
                return Unauthorized(ApiResponse<object>.CreateError(HttpErrorMessages.UserIdentityNotFound));
            }

            await _issueService.AddVoteAsync(id, userId, request.VoteType);

            // Fetch updated issue to return current vote counts
            var updatedIssue = await _issueService.GetIssueByIdAsync(id);

            return Ok(ApiResponse<object>.CreateSuccess(
                new { 
                    message = "Vote recorded successfully",
                    upvotes = updatedIssue?.Upvotes ?? 0,
                    downvotes = updatedIssue?.Downvotes ?? 0
                },
                "Vote recorded"
            ));
        }
        catch (InvalidOperationException)
        {
            return BadRequest(ApiResponse<object>.CreateError("Request could not be processed"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error voting on issue {IssueId}", id);
            return BadRequest(ApiResponse<object>.CreateError(HttpErrorMessages.FailedToRecordVote));
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
                return Unauthorized(ApiResponse<object>.CreateError(HttpErrorMessages.UserIdentityNotFound));
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
            return BadRequest(ApiResponse<object>.CreateError(HttpErrorMessages.FailedToRemoveVote));
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
                return Unauthorized(ApiResponse<object>.CreateError(HttpErrorMessages.UserIdentityNotFound));
            }

            var issue = await _issueService.GetIssueByIdAsync(id);
            if (issue == null)
            {
                return NotFound(ApiResponse<object>.CreateError(HttpErrorMessages.IssueNotFound));
            }

            // Only allow deletion by issue reporter or admins
            if (issue.Reporter.Id != userId && !User.IsInRole(RoleNames.Admin))
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
        catch (InvalidOperationException)
        {
            return BadRequest(ApiResponse<object>.CreateError("Request could not be processed"));
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

    /// <summary>
    /// Get AI analysis for an issue (used by frontend polling)
    /// </summary>
    /// <param name="id">Issue ID</param>
    /// <returns>AI analysis if available</returns>
    [HttpGet("{id}/analysis")]
    [ProducesResponseType(typeof(FixIt.Models.AI.IssueAnalysis), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetIssueAnalysis(string id)
    {
        try
        {
            var analysis = await _analysisService.GetAnalysisAsync(id);
            if (analysis == null)
            {
                return NoContent(); // 204 - Still processing
            }

            return Ok(analysis);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching analysis for issue {IssueId}", id);
            return BadRequest(ApiResponse<object>.CreateError("Failed to fetch analysis"));
        }
    }

    /// <summary>
    /// Add a comment to an issue
    /// </summary>
    /// <param name="issueId">Issue ID</param>
    /// <param name="request">Comment creation request</param>
    /// <returns>The created comment</returns>
    [HttpPost("{issueId}/comments")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CommentResponse>), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CommentResponse>>> AddComment(
        string issueId,
        [FromBody] CreateCommentRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(ApiResponse<object>.CreateError("User identity not found"));
            }

            // Use the user's privacy setting from their profile, not the request flag
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Unauthorized(ApiResponse<object>.CreateError("User not found"));
            }
            
            bool isAnonymous = user.AnonymousReportingEnabled;

            var comment = await _issueService.AddCommentAsync(
                issueId,
                userId,
                request.Text,
                isAnonymous
            );

            _logger.LogInformation(
                request.IsAnonymous
                    ? "Anonymous comment added to issue {IssueId}"
                    : "Comment added to issue {IssueId} by user {UserId}",
                issueId, userId);

            return CreatedAtAction(
                nameof(GetComments),
                new { issueId = issueId },
                ApiResponse<CommentResponse>.CreateSuccess(
                    comment.ToResponse(),
                    "Comment added successfully"
                )
            );
        }
        catch (ArgumentException)
        {
            return BadRequest(ApiResponse<object>.CreateError("Request could not be processed"));
        }
        catch (InvalidOperationException)
        {
            return BadRequest(ApiResponse<object>.CreateError("Request could not be processed"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding comment to issue {IssueId}", issueId);
            return BadRequest(ApiResponse<object>.CreateError("Failed to add comment"));
        }
    }

    /// <summary>
    /// Get all comments for an issue
    /// </summary>
    /// <param name="issueId">Issue ID</param>
    /// <returns>List of comments</returns>
    [HttpGet("{issueId}/comments")]
    [ProducesResponseType(typeof(ApiResponse<IEnumerable<CommentResponse>>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<IEnumerable<CommentResponse>>>> GetComments(string issueId)
    {
        try
        {
            var issue = await _issueService.GetIssueByIdAsync(issueId);
            if (issue == null)
            {
                return NotFound(ApiResponse<object>.CreateError("Issue not found"));
            }

            var comments = await _issueService.GetCommentsForIssueAsync(issueId);

            return Ok(ApiResponse<IEnumerable<CommentResponse>>.CreateSuccess(
                comments.Select(c => c.ToResponse()),
                "Comments retrieved successfully"
            ));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching comments for issue {IssueId}", issueId);
            return BadRequest(ApiResponse<object>.CreateError("Failed to fetch comments"));
        }
    }

    /// <summary>
    /// Delete a comment
    /// </summary>
    /// <param name="issueId">Issue ID</param>
    /// <param name="commentId">Comment ID</param>
    /// <returns>Success response</returns>
    [HttpDelete("{issueId}/comments/{commentId}")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<object>>> DeleteComment(string issueId, string commentId)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(ApiResponse<object>.CreateError("User identity not found"));
            }

            var comments = await _issueService.GetCommentsForIssueAsync(issueId);
            var comment = comments?.FirstOrDefault(c => c.Id == commentId);
            if (comment == null)
            {
                return NotFound(ApiResponse<object>.CreateError("Comment not found"));
            }

            // Only the comment author can delete their own comments
            if (comment.AuthorId != userId)
            {
                return Forbid();
            }

            await _issueService.DeleteCommentAsync(issueId, commentId);

            _logger.LogInformation("Comment {CommentId} deleted by user {UserId}", commentId, userId);

            return Ok(ApiResponse<object>.CreateSuccess(new { message = "Comment deleted successfully" }, "Comment deleted successfully"));
        }
        catch (InvalidOperationException)
        {
            return NotFound(ApiResponse<object>.CreateError("Request could not be processed"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting comment {CommentId}", commentId);
            return BadRequest(ApiResponse<object>.CreateError("Failed to delete comment"));
        }
    }

    /// <summary>
    /// Like a comment
    /// </summary>
    /// <param name="issueId">Issue ID</param>
    /// <param name="commentId">Comment ID</param>
    /// <returns>Updated comment with like counts</returns>
    [HttpPost("{issueId}/comments/{commentId}/like")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CommentLikeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CommentLikeResponse>>> LikeComment(string issueId, string commentId)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(ApiResponse<object>.CreateError("User identity not found"));
            }

            var comments = await _issueService.GetCommentsForIssueAsync(issueId);
            var comment = comments?.FirstOrDefault(c => c.Id == commentId);
            if (comment == null)
            {
                return NotFound(ApiResponse<object>.CreateError("Comment not found"));
            }

            await _issueService.LikeCommentAsync(issueId, commentId, userId);

            // Fetch updated comment
            var updatedComments = await _issueService.GetCommentsForIssueAsync(issueId);
            var updatedComment = updatedComments?.FirstOrDefault(c => c.Id == commentId);

            return Ok(ApiResponse<CommentLikeResponse>.CreateSuccess(
                new CommentLikeResponse
                {
                    LikeCount = updatedComment?.LikedBy?.Count ?? 0,
                    DislikeCount = updatedComment?.DislikedBy?.Count ?? 0
                },
                "Comment liked successfully"
            ));
        }
        catch (InvalidOperationException)
        {
            return NotFound(ApiResponse<object>.CreateError("Request could not be processed"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error liking comment {CommentId}", commentId);
            return BadRequest(ApiResponse<object>.CreateError("Failed to like comment"));
        }
    }

    /// <summary>
    /// Dislike a comment
    /// </summary>
    /// <param name="issueId">Issue ID</param>
    /// <param name="commentId">Comment ID</param>
    /// <returns>Updated comment with dislike counts</returns>
    [HttpPost("{issueId}/comments/{commentId}/dislike")]
    [Authorize]
    [ProducesResponseType(typeof(ApiResponse<CommentLikeResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ApiResponse<CommentLikeResponse>>> DislikeComment(string issueId, string commentId)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(ApiResponse<object>.CreateError("User identity not found"));
            }

            var comments = await _issueService.GetCommentsForIssueAsync(issueId);
            var comment = comments?.FirstOrDefault(c => c.Id == commentId);
            if (comment == null)
            {
                return NotFound(ApiResponse<object>.CreateError("Comment not found"));
            }

            await _issueService.DislikeCommentAsync(issueId, commentId, userId);

            // Fetch updated comment
            var updatedComments = await _issueService.GetCommentsForIssueAsync(issueId);
            var updatedComment = updatedComments?.FirstOrDefault(c => c.Id == commentId);

            return Ok(ApiResponse<CommentLikeResponse>.CreateSuccess(
                new CommentLikeResponse
                {
                    LikeCount = updatedComment?.LikedBy?.Count ?? 0,
                    DislikeCount = updatedComment?.DislikedBy?.Count ?? 0
                },
                "Comment disliked successfully"
            ));
        }
        catch (InvalidOperationException)
        {
            return NotFound(ApiResponse<object>.CreateError("Request could not be processed"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disliking comment {CommentId}", commentId);
            return BadRequest(ApiResponse<object>.CreateError("Failed to dislike comment"));
        }
    }
}

/// <summary>
/// Response model for comment like/dislike operations
/// </summary>
public class CommentLikeResponse
{
    public int LikeCount { get; set; }
    public int DislikeCount { get; set; }
}
