using FixIt.Models.Issues;
using FixIt.Models.Common;
using FixIt.Models.Enums;
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
        IEnumerable<string>? tagNames = null);

    Task<Issue?> GetIssueByIdAsync(string issueId);

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
        int page = 1,
        int pageSize = 20);

    Task UpdateIssueAsync(Issue issue);
}