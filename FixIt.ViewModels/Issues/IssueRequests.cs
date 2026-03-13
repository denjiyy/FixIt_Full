using System.ComponentModel.DataAnnotations;
using FixIt.Models.Enums;

namespace FixIt.ViewModels.Issues;

/// <summary>
/// Request model for creating a new issue
/// </summary>
public class CreateIssueRequest
{
    [Required(ErrorMessage = "Title is required")]
    [StringLength(200, MinimumLength = 3, ErrorMessage = "Title must be between 3 and 200 characters")]
    public string Title { get; set; } = null!;

    [Required(ErrorMessage = "Description is required")]
    [StringLength(5000, MinimumLength = 10, ErrorMessage = "Description must be between 10 and 5000 characters")]
    public string Description { get; set; } = null!;

    [Required(ErrorMessage = "Longitude is required")]
    [Range(-180, 180, ErrorMessage = "Longitude must be between -180 and 180")]
    public double Longitude { get; set; }

    [Required(ErrorMessage = "Latitude is required")]
    [Range(-90, 90, ErrorMessage = "Latitude must be between -90 and 90")]
    public double Latitude { get; set; }

    [Required(ErrorMessage = "City ID is required")]
    public string CityId { get; set; } = null!;

    public string? Address { get; set; }

    [StringLength(5000, ErrorMessage = "Tags must not exceed 5000 characters")]
    public string? TagsJson { get; set; } // Comma-separated or JSON array of tag names

    /// <summary>
    /// Whether to post the issue anonymously
    /// </summary>
    public bool IsAnonymous { get; set; } = false;
}

/// <summary>
/// Request model for updating issue status
/// </summary>
public class UpdateIssueStatusRequest
{
    [Required(ErrorMessage = "New status is required")]
    public IssueStatus NewStatus { get; set; }

    [StringLength(500, ErrorMessage = "Comment must not exceed 500 characters")]
    public string? Comment { get; set; }
}

/// <summary>
/// Request model for updating issue priority
/// </summary>
public class UpdateIssuePriorityRequest
{
    [Required(ErrorMessage = "Priority is required")]
    public IssuePriority Priority { get; set; }
}

/// <summary>
/// Request model for voting on an issue
/// </summary>
public class VoteRequest
{
    [Required(ErrorMessage = "Vote type is required")]
    public VoteType VoteType { get; set; }
}

/// <summary>
/// Request model for searching issues
/// </summary>
public class SearchIssuesRequest
{
    public string? SearchQuery { get; set; }
    public IEnumerable<string>? TagIds { get; set; }
    public IssueStatus? Status { get; set; }
    public IssuePriority? Priority { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

/// <summary>
/// Request model for creating or updating a comment
/// </summary>
public class CreateCommentRequest
{
    [Required(ErrorMessage = "Comment text is required")]
    [StringLength(5000, MinimumLength = 1, ErrorMessage = "Comment must be between 1 and 5000 characters")]
    public string Text { get; set; } = null!;

    /// <summary>
    /// Whether to post the comment anonymously
    /// </summary>
    public bool IsAnonymous { get; set; } = false;

    /// <summary>
    /// Optional list of media IDs to attach to the comment
    /// </summary>
    public IEnumerable<string>? MediaIds { get; set; }
}
