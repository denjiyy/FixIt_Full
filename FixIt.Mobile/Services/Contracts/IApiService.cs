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
    Task<ApiResult> LikeCommentAsync(string issueId, string commentId, CancellationToken ct = default);
    Task<ApiResult> DislikeCommentAsync(string issueId, string commentId, CancellationToken ct = default);
    Task<List<SafetyHazard>> GetCriticalHazardsAsync(CancellationToken ct = default);
    Task<ApiResult> ConfirmHazardAsync(string hazardId, CancellationToken ct = default);
    Task<LeaderboardResult> GetLeaderboardAsync(string period, CancellationToken ct = default);
    Task<CityHealthReport> GetHealthReportAsync(string cityId, CancellationToken ct = default);
    Task<IssueAnalysis?> GetAnalysisAsync(string issueId, CancellationToken ct = default);
    Task<IssueFilterResult?> TranslateNaturalLanguageFilterAsync(string query, CancellationToken ct = default);
    Task<PublicUserProfile?> GetPublicProfileAsync(string userId, CancellationToken ct = default);

    // AI / suggestions
    Task<DraftSuggestion?> GetDraftSuggestionsAsync(string title, string description, CancellationToken ct = default);
    Task<HazardClusterInsight?> GetHazardClusterInsightAsync(IEnumerable<string> hazardIds, CancellationToken ct = default);
    IAsyncEnumerable<string> StreamIssueSummaryAsync(string issueId, CancellationToken ct = default);

    // Safety preferences
    Task<ApiResult> ToggleAnonymousReportingAsync(bool enabled, CancellationToken ct = default);
    Task<AlertPreferences?> GetAlertPreferencesAsync(CancellationToken ct = default);
    Task<ApiResult> SaveAlertPreferencesAsync(AlertPreferences preferences, CancellationToken ct = default);

    // User profile
    Task<ApiResult> SetProfileVisibilityAsync(string visibility, CancellationToken ct = default);

    // Geocoding
    Task<ReverseGeocodeResult?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct = default);

    // Tags
    Task<List<Tag>> GetPopularTagsAsync(int limit = 20, CancellationToken ct = default);
    Task<List<Issue>> GetIssuesByTagAsync(string tagName, int page = 1, int pageSize = 20, CancellationToken ct = default);

    // Issue editing
    Task<ApiResult> UpdateIssueAsync(string issueId, string title, string description, string address, CancellationToken ct = default);

    // Auth
    Task<ApiResult> ForgotPasswordAsync(string email, CancellationToken ct = default);

    // Cities
    Task<List<CityInfo>> GetCitiesAsync(CancellationToken ct = default);

    // Hazard management
    Task<ApiResult> ReportHazardAsync(string type, string severity, string title, string description, double latitude, double longitude, CancellationToken ct = default);

    // Email preferences
    Task<EmailPreferences?> GetEmailPreferencesAsync(CancellationToken ct = default);
    Task<ApiResult> SaveEmailPreferencesAsync(EmailPreferences preferences, CancellationToken ct = default);

    // City preference
    Task<string?> GetCityPreferenceAsync(CancellationToken ct = default);
    Task<ApiResult> SaveCityPreferenceAsync(string cityId, CancellationToken ct = default);
}
