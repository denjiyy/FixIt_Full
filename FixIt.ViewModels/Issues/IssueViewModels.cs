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
/// Response model for issue summary
/// </summary>
public class IssueSummaryResponse
{
    public string Id { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string CityId { get; set; } = null!;
    public IssueStatus Status { get; set; }
    public IssuePriority Priority { get; set; }
    public int Upvotes { get; set; }
    public int Downvotes { get; set; }
    public int CommentCount { get; set; }
    public int ViewCount { get; set; }
    public UserSummaryResponse Reporter { get; set; } = null!;
    public IEnumerable<string> TagIds { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivityAt { get; set; }

    public int VoteScore => Upvotes - Downvotes;
}

/// <summary>
/// Response model for detailed issue information
/// </summary>
public class IssueDetailResponse
{
    public string Id { get; set; } = null!;
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string CityId { get; set; } = null!;
    public string? Address { get; set; }
    public double Longitude { get; set; }
    public double Latitude { get; set; }
    public IssueStatus Status { get; set; }
    public IssuePriority Priority { get; set; }
    public int Upvotes { get; set; }
    public int Downvotes { get; set; }
    public int CommentCount { get; set; }
    public int ViewCount { get; set; }
    public UserSummaryResponse Reporter { get; set; } = null!;
    public IEnumerable<string> TagIds { get; set; } = Array.Empty<string>();
    public IEnumerable<IssueStatusHistoryResponse> StatusHistory { get; set; } = Array.Empty<IssueStatusHistoryResponse>();
    public IEnumerable<string> MediaIds { get; set; } = Array.Empty<string>();
    public bool IsPinned { get; set; }
    public bool IsLocked { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime LastActivityAt { get; set; }

    public int VoteScore => Upvotes - Downvotes;
}

/// <summary>
/// Response model for issue status history
/// </summary>
public class IssueStatusHistoryResponse
{
    public IssueStatus From { get; set; }
    public IssueStatus To { get; set; }
    public string ChangedByUserId { get; set; } = null!;
    public string? Comment { get; set; }
    public DateTime ChangedAt { get; set; }
}

/// <summary>
/// Response model for user summary
/// </summary>
public class UserSummaryResponse
{
    public string Id { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
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
