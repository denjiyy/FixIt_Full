using FixIt.Mobile.Models;

namespace FixIt.Mobile.Services.Contracts;

public interface IApiService
{
    Task<List<Issue>> GetIssuesAsync(string? filter = null, string? search = null, int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<Issue?> GetIssueAsync(string id, CancellationToken ct = default);
    Task<ApiResult> ReportIssueAsync(ReportIssueRequest request, CancellationToken ct = default);
    Task<List<Issue>> GetMyIssuesAsync(int page = 1, int pageSize = 20, CancellationToken ct = default);
    Task<ApiResult> DeleteIssueAsync(string issueId, CancellationToken ct = default);
    Task<ApiResult> VoteAsync(string issueId, bool upvote, CancellationToken ct = default);
    Task<UserInfo?> GetCurrentUserAsync(CancellationToken ct = default);
    Task<List<Comment>> GetCommentsAsync(string issueId, CancellationToken ct = default);
    Task<Comment?> AddCommentAsync(string issueId, string text, CancellationToken ct = default);
    Task<List<SafetyHazard>> GetCriticalHazardsAsync(CancellationToken ct = default);
    Task<ApiResult> ConfirmHazardAsync(string hazardId, CancellationToken ct = default);
}
