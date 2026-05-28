using FixIt.Models.Issues;
using FixIt.Models.Common;
using FixIt.Models.Enums;
using FixIt.Models.AI;
using FixIt.Data.Repository.Contracts;

namespace FixIt.Services.Contracts;

public interface IIssueService
{
    Task<Issue> CreateIssueAsync(
        string title,
        string description,
        double longitude,
        double latitude,
        string cityId,
        UserSummary reporter,
        IEnumerable<string>? tagNames = null,
        bool isAnonymous = false,
        IssuePriority? priority = null,
        IssueCategory? category = null,
        string? department = null,
        string? address = null);

    Task<Issue?> GetIssueByIdAsync(string issueId);

    /// <summary>
    /// Track a view event for an issue to prevent duplicate view counting.
    /// Only increments the view count if this is a new view from a unique user/session.
    /// </summary>
    /// <param name="issueId">The issue ID</param>
    /// <param name="userId">The user ID (can be null for anonymous users)</param>
    /// <param name="sessionId">Session ID for tracking anonymous users</param>
    /// <param name="ipAddress">IP address for additional tracking</param>
    /// <returns>True if view was recorded, false if it was a duplicate</returns>
    Task<bool> TrackViewAsync(string issueId, string? userId = null, string? sessionId = null, string? ipAddress = null);

    Task<PagedResult<Issue>> GetIssuesByCityAsync(
        string cityId,
        IssueStatus? status = null,
        int page = 1,
        int pageSize = 20);

    Task<PagedResult<Issue>> SearchIssuesAsync(
        string cityId,
        string? searchQuery = null,
        IEnumerable<string>? tagIds = null,
        IssueStatus? status = null,
        IssuePriority? priority = null,
        int page = 1,
        int pageSize = 20);

    Task<PagedResult<Issue>> GetUserIssuesAsync(string userId, int page = 1, int pageSize = 20);

    Task UpdateIssueStatusAsync(
        string issueId,
        IssueStatus newStatus,
        string changedByUserId,
        string? comment = null);

    Task UpdateIssuePriorityAsync(string issueId, IssuePriority priority);

    Task AddVoteAsync(string issueId, string userId, VoteType voteType);

    Task RemoveVoteAsync(string issueId, string userId);
    Task DeleteIssueAsync(string issueId);

    Task SoftDeleteIssueAsync(string issueId);

    Task RestoreIssueAsync(string issueId);

    Task<List<Issue>> GetIssuesByCityAsync(string cityId);

    Task<int> GetIssueCountByCityAsync(string cityId);

    Task<PagedResult<Issue>> GetAllIssuesAsync(
        string? searchQuery = null,
        IssueStatus? status = null,
        IssuePriority? priority = null,
        IssueCategory? category = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        IssueSortOption sort = IssueSortOption.Newest,
        int page = 1,
        int pageSize = 20);

    Task<IssuePublicOverview> GetPublicIssueOverviewAsync(int featuredCount = 3);

    Task<PagedResult<Issue>> GetIssuesByTagAsync(
        string tagId,
        int page = 1,
        int pageSize = 20);

    Task UpdateIssueAsync(Issue issue);

    Task<FixIt.Models.Engagement.Comment> AddCommentAsync(
        string issueId,
        string authorId,
        string text,
        bool isAnonymous = false);

    Task<List<FixIt.Models.Engagement.Comment>> GetCommentsForIssueAsync(string issueId);

    Task DeleteCommentAsync(string issueId, string commentId);

    Task LikeCommentAsync(string issueId, string commentId, string userId);

    Task DislikeCommentAsync(string issueId, string commentId, string userId);
}
