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
        string neighborhoodId,
        UserSummary reporter,
        IEnumerable<string>? tagNames = null);

    Task<Issue?> GetIssueByIdAsync(string issueId);

    Task<PagedResult<Issue>> GetIssuesByCityAsync(
        string cityId,
        IssueStatus? status = null,
        int page = 1,
        int pageSize = 20);

    Task<IEnumerable<Issue>> GetUserIssuesAsync(string userId);

    Task UpdateIssueStatusAsync(
        string issueId,
        IssueStatus newStatus,
        string changedByUserId,
        string? comment = null);

    Task DeleteIssueAsync(string issueId);
}