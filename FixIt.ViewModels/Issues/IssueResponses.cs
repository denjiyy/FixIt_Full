using FixIt.Models.Enums;

namespace FixIt.ViewModels.Issues;

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
    public bool IsAnonymous { get; set; }
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
    public bool IsAnonymous { get; set; }
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
/// Response model for a single comment
/// </summary>
public class CommentResponse
{
    public string Id { get; set; } = null!;
    public string IssueId { get; set; } = null!;
    public UserSummaryResponse Author { get; set; } = null!;
    public bool IsAnonymous { get; set; }
    public string Text { get; set; } = null!;
    public IEnumerable<string> MediaIds { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
}
