using FixIt.Models.Enums;

namespace FixIt.Services.Analytics.Models;

/// <summary>
/// Summary information for a top issue
/// </summary>
public class TopIssueInfo
{
    public string IssueId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Upvotes { get; set; }
    public int Comments { get; set; }
    public IssueStatus Status { get; set; }
    public IssuePriority Priority { get; set; }
}
