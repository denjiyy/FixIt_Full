using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.Services;

public class ApiService : IApiService
{
    private readonly IConnectivityService _connectivity;
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions;
    // Bounded caches replace the previously unbounded Dictionary<,>'s that
    // would grow indefinitely over a long session. Capacities chosen for
    // typical browse-back-and-forth navigation: ~50 issue details and ~30
    // comment threads. ApiService is registered as a singleton in MauiProgram.
    private readonly BoundedCache<string, Issue> _issueCache = new(capacity: 50);
    private readonly BoundedCache<string, List<Comment>> _commentsCache = new(capacity: 30);
    private List<Issue> _cachedIssues = [];
    private List<Issue> _cachedMyIssues = [];
    private List<SafetyHazard> _cachedHazards = [];

    public ApiService(IHttpClientFactory httpClientFactory, JsonSerializerOptions jsonOptions, IConnectivityService connectivity)
    {
        _httpClient = httpClientFactory.CreateClient(AppConstants.ApiClientName);
        _jsonOptions = jsonOptions;
        _connectivity = connectivity;
    }

    public async Task<List<Issue>> GetIssuesAsync(
        string? filter = null,
        string? search = null,
        int page = 1,
        int pageSize = MobileSettings.PaginationPageSize,
        CancellationToken ct = default)
    {
        if (!_connectivity.IsOnline)
        {
            return ApplyLocalIssueFilters(_cachedIssues, filter, search, page, pageSize);
        }

        try
        {
            var normalizedFilter = NormalizeFilter(filter);
            List<Issue> issues;

            if (!string.IsNullOrWhiteSpace(search))
            {
                issues = await SearchIssuesAsync(normalizedFilter, search, page, pageSize, ct);
            }
            else
            {
                var path = $"{AppConstants.ApiIssues}/city/{AppConstants.DefaultCityId}?page={page}&pageSize={pageSize}";
                if (normalizedFilter.HasValue)
                {
                    path += $"&status={normalizedFilter.Value}";
                }

                using var response = await _httpClient.GetAsync(path, ct);
                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"[API] Failed to load issues. Status: {(int)response.StatusCode}");
                    return ApplyLocalIssueFilters(_cachedIssues, filter, search, page, pageSize);
                }

                var envelope = await DeserializeEnvelopeAsync<PaginatedEnvelope<IssueSummaryDto>>(response, ct);
                var items = envelope?.Data?.Items ?? [];
                issues = items.Select(MapIssue).ToList();
            }

            CacheIssues(issues, replaceListCache: page == 1 && string.IsNullOrWhiteSpace(search));
            return issues;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[API] Network error: {ex.Message}");
            return ApplyLocalIssueFilters(_cachedIssues, filter, search, page, pageSize);
        }
        catch (TaskCanceledException)
        {
            return [];
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[API] Parse error: {ex.Message}");
            return ApplyLocalIssueFilters(_cachedIssues, filter, search, page, pageSize);
        }
    }

    public async Task<Issue?> GetIssueAsync(string id, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        if (!_connectivity.IsOnline && _issueCache.TryGetValue(id, out var cachedIssue))
        {
            return cachedIssue;
        }

        try
        {
            using var response = await _httpClient.GetAsync($"{AppConstants.ApiIssues}/{Uri.EscapeDataString(id)}", ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[API] Failed to load issue. Status: {(int)response.StatusCode}");
                return _issueCache.GetValueOrDefault(id);
            }

            var envelope = await DeserializeEnvelopeAsync<IssueDetailDto>(response, ct);
            var issue = envelope?.Data == null ? null : MapIssue(envelope.Data);
            if (issue != null)
            {
                _issueCache[issue.Id] = issue;
            }

            return issue;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[API] Network error: {ex.Message}");
            return _issueCache.GetValueOrDefault(id);
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[API] Parse error: {ex.Message}");
            return _issueCache.GetValueOrDefault(id);
        }
    }

    public async Task<ApiResult> ReportIssueAsync(ReportIssueRequest request, CancellationToken ct = default)
    {
        if (!_connectivity.IsOnline)
        {
            return new ApiResult(false, Localization.LocalizationService.Get("Common_Offline"));
        }

        try
        {
            using var jsonResponse = await _httpClient.PostAsJsonAsync(AppConstants.ApiIssues, new
            {
                title = request.Title,
                description = request.Description,
                longitude = request.Longitude,
                latitude = request.Latitude,
                cityId = string.IsNullOrWhiteSpace(request.CityId) ? AppConstants.DefaultCityId : request.CityId,
                address = request.Address,
                tagsJson = string.Empty,
                isAnonymous = false
            }, _jsonOptions, ct);

            if (!jsonResponse.IsSuccessStatusCode)
            {
                var error = await ExtractApiErrorAsync(jsonResponse, Localization.LocalizationService.Get("Report_Error"), ct);
                return new ApiResult(false, error, (int)jsonResponse.StatusCode);
            }

            var createdIssueId = await ExtractCreatedIssueIdAsync(jsonResponse, ct);
            if (string.IsNullOrEmpty(createdIssueId) || request.Photos.Count == 0)
            {
                return ApiResult.Ok();
            }

            var failedUploads = 0;
            foreach (var photo in request.Photos)
            {
                ct.ThrowIfCancellationRequested();
                if (!await UploadIssueMediaAsync(createdIssueId, photo, ct))
                {
                    failedUploads++;
                }
            }

            return failedUploads == 0
                ? ApiResult.Ok()
                : new ApiResult(true, Localization.LocalizationService.Get("Report_PartialUploadWarning"));
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[API] Network error: {ex.Message}");
            return new ApiResult(false, Localization.LocalizationService.Get("Common_Error_Network"));
        }
        catch (TaskCanceledException)
        {
            return new ApiResult(false, Localization.LocalizationService.Get("Common_Error_Generic"));
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[API] Parse error: {ex.Message}");
            return new ApiResult(false, Localization.LocalizationService.Get("Common_Error_Generic"));
        }
    }

    private async Task<bool> UploadIssueMediaAsync(string issueId, PhotoAttachment photo, CancellationToken ct)
    {
        try
        {
            using var content = new MultipartFormDataContent();
            var bytesContent = new ByteArrayContent(photo.Bytes);
            bytesContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(photo.ContentType);
            content.Add(bytesContent, "file", photo.FileName);

            using var response = await _httpClient.PostAsync($"{AppConstants.ApiIssues}/{issueId}/media", content, ct);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[API] Media upload failed for issue {issueId}: {(int)response.StatusCode}");
                return false;
            }
            return true;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            Console.WriteLine($"[API] Media upload error for issue {issueId}: {ex.Message}");
            return false;
        }
    }

    private async Task<string?> ExtractCreatedIssueIdAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("data", out var data) &&
                data.ValueKind == JsonValueKind.Object &&
                data.TryGetProperty("id", out var idElement) &&
                idElement.ValueKind == JsonValueKind.String)
            {
                return idElement.GetString();
            }

            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("id", out var topLevelId) &&
                topLevelId.ValueKind == JsonValueKind.String)
            {
                return topLevelId.GetString();
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[API] Failed to parse created issue id: {ex.Message}");
        }

        return null;
    }

    public async Task<List<Issue>> GetMyIssuesAsync(
        int page = 1,
        int pageSize = MobileSettings.PaginationPageSize,
        CancellationToken ct = default)
    {
        if (!_connectivity.IsOnline)
        {
            return _cachedMyIssues.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        }

        try
        {
            using var response = await _httpClient.GetAsync($"{AppConstants.ApiIssues}/my-issues?page={page}&pageSize={pageSize}", ct);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[API] Failed to load my issues. Status: {(int)response.StatusCode}");
                return [];
            }

            var envelope = await DeserializeEnvelopeAsync<PaginatedEnvelope<IssueSummaryDto>>(response, ct);
            var issues = (envelope?.Data?.Items ?? []).Select(MapIssue).ToList();
            if (page == 1)
            {
                _cachedMyIssues = issues;
            }
            else
            {
                _cachedMyIssues.AddRange(issues);
            }

            CacheIssues(issues, replaceListCache: false);
            return issues;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[API] Network error: {ex.Message}");
            return [];
        }
        catch (TaskCanceledException)
        {
            return [];
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[API] Parse error: {ex.Message}");
            return [];
        }
    }

    public async Task<ApiResult> DeleteIssueAsync(string issueId, CancellationToken ct = default)
    {
        if (!_connectivity.IsOnline)
        {
            return new ApiResult(false, Localization.LocalizationService.Get("Common_Offline"));
        }

        try
        {
            using var response = await _httpClient.DeleteAsync($"{AppConstants.ApiIssues}/{Uri.EscapeDataString(issueId)}", ct);
            if (!response.IsSuccessStatusCode)
            {
                var error = await ExtractApiErrorAsync(response, Localization.LocalizationService.Get("Common_Error_Generic"), ct);
                return new ApiResult(false, error);
            }

            _issueCache.Remove(issueId);
            _cachedIssues = _cachedIssues.Where(i => i.Id != issueId).ToList();
            _cachedMyIssues = _cachedMyIssues.Where(i => i.Id != issueId).ToList();
            return new ApiResult(true);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[API] Network error: {ex.Message}");
            return new ApiResult(false, Localization.LocalizationService.Get("Common_Error_Network"));
        }
        catch (TaskCanceledException)
        {
            return new ApiResult(false, Localization.LocalizationService.Get("Common_Error_Generic"));
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[API] Parse error: {ex.Message}");
            return new ApiResult(false, Localization.LocalizationService.Get("Common_Error_Generic"));
        }
    }

    public async Task<ApiResult> VoteAsync(string issueId, bool upvote, CancellationToken ct = default)
    {
        if (!_connectivity.IsOnline)
        {
            return new ApiResult(false, Localization.LocalizationService.Get("Common_Offline"));
        }

        try
        {
            using var response = await _httpClient.PostAsJsonAsync($"{AppConstants.ApiIssues}/{Uri.EscapeDataString(issueId)}/vote", new
            {
                voteType = upvote ? "upvote" : "downvote"
            }, _jsonOptions, ct);

            var voteSucceeded = response.IsSuccessStatusCode;
            if (!voteSucceeded)
            {
                using var numericResponse = await _httpClient.PostAsJsonAsync($"{AppConstants.ApiIssues}/{Uri.EscapeDataString(issueId)}/vote", new
                {
                    voteType = upvote ? 1 : -1
                }, _jsonOptions, ct);

                voteSucceeded = numericResponse.IsSuccessStatusCode;
                if (!voteSucceeded)
                {
                    var numericError = await ExtractApiErrorAsync(numericResponse, Localization.LocalizationService.Get("Common_Error_Generic"), ct);
                    return new ApiResult(false, numericError);
                }
            }

            // FIX B-10: mutate cached vote state only after one of the accepted API payloads succeeds.
            if (voteSucceeded && _issueCache.TryGetValue(issueId, out var issue))
            {
                issue.UserHasUpvoted = upvote;
                issue.UserHasDownvoted = !upvote;
                issue.VoteCount += upvote ? 1 : -1;
            }

            return new ApiResult(true);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[API] Network error: {ex.Message}");
            return new ApiResult(false, Localization.LocalizationService.Get("Common_Error_Network"));
        }
        catch (TaskCanceledException)
        {
            return new ApiResult(false, Localization.LocalizationService.Get("Common_Error_Generic"));
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[API] Parse error: {ex.Message}");
            return new ApiResult(false, Localization.LocalizationService.Get("Common_Error_Generic"));
        }
    }

    public async Task<UserInfo?> GetCurrentUserAsync(CancellationToken ct = default)
    {
        if (!_connectivity.IsOnline)
        {
            return null;
        }

        try
        {
            using var response = await _httpClient.GetAsync($"{AppConstants.ApiAuth}/user", ct);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[API] Failed to load current user. Status: {(int)response.StatusCode}");
                return null;
            }

            var envelope = await DeserializeEnvelopeAsync<UserInfoDto>(response, ct);
            if (envelope?.Data == null)
            {
                return null;
            }

            return new UserInfo
            {
                Id = envelope.Data.Id is { ValueKind: JsonValueKind.String } idEl ? idEl.GetString() ?? string.Empty : string.Empty,
                Email = envelope.Data.Email ?? string.Empty,
                DisplayName = envelope.Data.DisplayName ?? string.Empty,
                ReputationPoints = envelope.Data.ReputationScore ?? 0,
                TrustLevel = envelope.Data.TrustLevel?.ToString() ?? "Community"
            };
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[API] Network error: {ex.Message}");
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[API] Parse error: {ex.Message}");
            return null;
        }
    }

    public async Task<List<Comment>> GetCommentsAsync(string issueId, CancellationToken ct = default)
    {
        if (!_connectivity.IsOnline && _commentsCache.TryGetValue(issueId, out var cachedComments))
        {
            return cachedComments;
        }

        try
        {
            using var response = await _httpClient.GetAsync($"{AppConstants.ApiIssues}/{Uri.EscapeDataString(issueId)}/comments", ct);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[API] Failed to load comments. Status: {(int)response.StatusCode}");
                return _commentsCache.GetValueOrDefault(issueId) ?? [];
            }

            var envelope = await DeserializeEnvelopeAsync<List<CommentDto>>(response, ct);
            var comments = (envelope?.Data ?? []).Select(MapComment).ToList();
            _commentsCache[issueId] = comments;
            return comments;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[API] Network error: {ex.Message}");
            return _commentsCache.GetValueOrDefault(issueId) ?? [];
        }
        catch (TaskCanceledException)
        {
            return [];
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[API] Parse error: {ex.Message}");
            return _commentsCache.GetValueOrDefault(issueId) ?? [];
        }
    }

    public async Task<Comment?> AddCommentAsync(string issueId, string text, CancellationToken ct = default)
    {
        if (!_connectivity.IsOnline)
        {
            return null;
        }

        try
        {
            using var response = await _httpClient.PostAsJsonAsync($"{AppConstants.ApiIssues}/{Uri.EscapeDataString(issueId)}/comments", new
            {
                text,
                isAnonymous = false
            }, _jsonOptions, ct);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[API] Failed to add comment. Status: {(int)response.StatusCode}");
                return null;
            }

            var envelope = await DeserializeEnvelopeAsync<CommentDto>(response, ct);
            var comment = envelope?.Data == null ? null : MapComment(envelope.Data);
            if (comment != null)
            {
                if (!_commentsCache.TryGetValue(issueId, out var comments))
                {
                    comments = [];
                    _commentsCache[issueId] = comments;
                }

                comments.Add(comment);
            }

            return comment;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[API] Network error: {ex.Message}");
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[API] Parse error: {ex.Message}");
            return null;
        }
    }

    public async Task<List<SafetyHazard>> GetCriticalHazardsAsync(CancellationToken ct = default)
    {
        if (!_connectivity.IsOnline)
        {
            return _cachedHazards;
        }

        try
        {
            using var response = await _httpClient.GetAsync($"{AppConstants.ApiSafety}/critical-hazards?cityId={AppConstants.DefaultCityId}", ct);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[API] Failed to load alerts. Status: {(int)response.StatusCode}");
                return _cachedHazards;
            }

            var envelope = await DeserializeEnvelopeAsync<List<HazardDto>>(response, ct);
            var hazards = (envelope?.Data ?? []).Select(MapHazard).ToList();
            _cachedHazards = hazards;
            return hazards;
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[API] Network error: {ex.Message}");
            return _cachedHazards;
        }
        catch (TaskCanceledException)
        {
            return [];
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[API] Parse error: {ex.Message}");
            return _cachedHazards;
        }
    }

    public async Task<ApiResult> ConfirmHazardAsync(string hazardId, CancellationToken ct = default)
    {
        if (!_connectivity.IsOnline)
        {
            return new ApiResult(false, Localization.LocalizationService.Get("Common_Offline"));
        }

        try
        {
            using var response = await _httpClient.PostAsync($"{AppConstants.ApiSafety}/{Uri.EscapeDataString(hazardId)}/confirm", null, ct);
            if (response.IsSuccessStatusCode)
            {
                return new ApiResult(true);
            }

            var error = await ExtractApiErrorAsync(response, Localization.LocalizationService.Get("Common_Error_Generic"), ct);
            return new ApiResult(false, error);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[API] Network error: {ex.Message}");
            return new ApiResult(false, Localization.LocalizationService.Get("Common_Error_Network"));
        }
        catch (TaskCanceledException)
        {
            return new ApiResult(false, Localization.LocalizationService.Get("Common_Error_Generic"));
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[API] Parse error: {ex.Message}");
            return new ApiResult(false, Localization.LocalizationService.Get("Common_Error_Generic"));
        }
    }

    public async Task<LeaderboardResult> GetLeaderboardAsync(string period, CancellationToken ct = default)
    {
        if (!_connectivity.IsOnline)
        {
            return new LeaderboardResult();
        }

        try
        {
            var normalizedPeriod = period switch
            {
                "weekly" => "weekly",
                "monthly" => "monthly",
                "alltime" => "alltime",
                _ => "weekly"
            };

            using var response = await _httpClient.GetAsync($"api/leaderboards?period={normalizedPeriod}", ct);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[API] Leaderboard endpoint unavailable. Status: {(int)response.StatusCode}");
                return new LeaderboardResult();
            }

            var entries = await DeserializeFlexibleAsync<List<LeaderboardEntryDto>>(response, ct, "entries", "items", "leaderboard");
            return new LeaderboardResult
            {
                Entries = (entries ?? []).Select(MapLeaderboardEntry).OrderBy(e => e.Rank).ToList()
            };
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[API] Network error: {ex.Message}");
            return new LeaderboardResult();
        }
        catch (TaskCanceledException)
        {
            return new LeaderboardResult();
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[API] Parse error: {ex.Message}");
            return new LeaderboardResult();
        }
    }

    public async Task<CityHealthReport> GetHealthReportAsync(string cityId, CancellationToken ct = default)
    {
        if (!_connectivity.IsOnline)
        {
            return new CityHealthReport();
        }

        try
        {
            using var response = await _httpClient.GetAsync($"api/health-reports/{Uri.EscapeDataString(cityId)}", ct);
            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[API] Health report endpoint unavailable. Status: {(int)response.StatusCode}");
                return new CityHealthReport();
            }

            return await DeserializeFlexibleAsync<CityHealthReport>(response, ct, "report", "healthReport")
                ?? new CityHealthReport();
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[API] Network error: {ex.Message}");
            return new CityHealthReport();
        }
        catch (TaskCanceledException)
        {
            return new CityHealthReport();
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[API] Parse error: {ex.Message}");
            return new CityHealthReport();
        }
    }

    public async Task<IssueAnalysis?> GetAnalysisAsync(string issueId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(issueId) || !_connectivity.IsOnline)
        {
            return null;
        }

        try
        {
            using var response = await _httpClient.GetAsync($"api/analysis/analyze/{Uri.EscapeDataString(issueId)}", ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[API] Analysis endpoint unavailable. Status: {(int)response.StatusCode}");
                return null;
            }

            var dto = await DeserializeFlexibleAsync<IssueAnalysisDto>(response, ct, "analysis");
            return dto == null ? null : MapIssueAnalysis(dto);
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[API] Network error: {ex.Message}");
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[API] Parse error: {ex.Message}");
            return null;
        }
    }

    public async Task<IssueFilterResult?> TranslateNaturalLanguageFilterAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || !_connectivity.IsOnline)
        {
            return null;
        }

        try
        {
            using var response = await _httpClient.PostAsJsonAsync("api/analysis/issue-search/translate", new
            {
                query = query.Trim()
            }, _jsonOptions, ct);

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[API] AI filter endpoint unavailable. Status: {(int)response.StatusCode}");
                return null;
            }

            return await DeserializeFlexibleAsync<IssueFilterResult>(response, ct, "filter", "result");
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[API] Network error: {ex.Message}");
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[API] Parse error: {ex.Message}");
            return null;
        }
    }

    public async Task<PublicUserProfile?> GetPublicProfileAsync(string userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId) || !_connectivity.IsOnline)
        {
            return null;
        }

        try
        {
            using var response = await _httpClient.GetAsync($"api/users/{Uri.EscapeDataString(userId)}/profile", ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            if (!response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[API] Public profile endpoint unavailable. Status: {(int)response.StatusCode}");
                return null;
            }

            return await DeserializeFlexibleAsync<PublicUserProfile>(response, ct, "profile", "user");
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[API] Network error: {ex.Message}");
            return null;
        }
        catch (TaskCanceledException)
        {
            return null;
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[API] Parse error: {ex.Message}");
            return null;
        }
    }

    private async Task<List<Issue>> SearchIssuesAsync(int? status, string search, int page, int pageSize, CancellationToken ct)
    {
        using var response = await _httpClient.PostAsJsonAsync(
            $"{AppConstants.ApiIssues}/city/{AppConstants.DefaultCityId}/search",
            new
            {
                searchQuery = search,
                status,
                page,
                pageSize
            },
            _jsonOptions,
            ct);

        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        var envelope = await DeserializeEnvelopeAsync<PaginatedEnvelope<IssueSummaryDto>>(response, ct);
        var items = envelope?.Data?.Items ?? [];
        return items.Select(MapIssue).ToList();
    }

    private static List<Issue> ApplyLocalIssueFilters(List<Issue> source, string? filter, string? search, int page, int pageSize)
    {
        IEnumerable<Issue> query = source;
        if (!string.IsNullOrWhiteSpace(filter) && !filter.Equals(AppConstants.FilterAll, StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(i => i.Status.Equals(filter, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(i =>
                i.Title.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                i.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                i.CityName.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        return query.Skip((page - 1) * pageSize).Take(pageSize).ToList();
    }

    private void CacheIssues(List<Issue> issues, bool replaceListCache)
    {
        if (replaceListCache)
        {
            _cachedIssues = issues;
        }
        else
        {
            foreach (var issue in issues)
            {
                var index = _cachedIssues.FindIndex(i => i.Id == issue.Id);
                if (index >= 0)
                {
                    _cachedIssues[index] = issue;
                }
                else
                {
                    _cachedIssues.Add(issue);
                }
            }
        }

        foreach (var issue in issues)
        {
            if (!string.IsNullOrWhiteSpace(issue.Id))
            {
                _issueCache[issue.Id] = issue;
            }
        }
    }

    private static int? NormalizeFilter(string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter) || filter.Equals(AppConstants.FilterAll, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return filter switch
        {
            AppConstants.FilterNew => AppConstants.StatusNewValue,
            AppConstants.FilterInProgress => AppConstants.StatusInProgressValue,
            AppConstants.FilterResolved => AppConstants.StatusFixedValue,
            _ => null
        };
    }

    private static Issue MapIssue(IssueSummaryDto dto)
    {
        var cityName = ResolveCityName(dto.CityId);
        var status = MapStatus(dto.Status, dto.Priority);

        return new Issue
        {
            Id = dto.Id ?? string.Empty,
            Title = dto.Title ?? string.Empty,
            Description = dto.Description ?? string.Empty,
            Status = status,
            Category = ResolveCategoryName(dto.Category),
            Priority = MapPriority(dto.Priority),
            CityName = cityName,
            Address = dto.Address ?? string.Empty,
            PhotoUrl = dto.PhotoUrl ?? dto.ImageUrl ?? dto.MediaUrl,
            Latitude = NormalizeCoordinate(dto.Latitude),
            Longitude = NormalizeCoordinate(dto.Longitude),
            CreatedAt = dto.CreatedAt,
            VoteCount = dto.VoteScore ?? (dto.Upvotes - dto.Downvotes),
            AuthorUserId = dto.Reporter?.Id,
            AuthorName = dto.Reporter?.DisplayName ?? "Anonymous",
            UserHasUpvoted = dto.UserVote > 0,
            UserHasDownvoted = dto.UserVote < 0
        };
    }

    private static Issue MapIssue(IssueDetailDto dto)
    {
        var issue = MapIssue((IssueSummaryDto)dto);
        issue.CityName = !string.IsNullOrWhiteSpace(dto.Address) ? ExtractCityNameFromAddress(dto.Address) : issue.CityName;
        issue.Address = dto.Address ?? issue.Address;
        return issue;
    }

    private static Comment MapComment(CommentDto dto)
    {
        return new Comment
        {
            Id = dto.Id ?? string.Empty,
            IssueId = dto.IssueId ?? string.Empty,
            AuthorName = dto.Author?.DisplayName ?? "Anonymous",
            Text = dto.Text ?? string.Empty,
            CreatedAt = dto.CreatedAt
        };
    }

    private static SafetyHazard MapHazard(HazardDto dto)
    {
        return new SafetyHazard
        {
            Id = dto.Id ?? string.Empty,
            Type = dto.Type ?? string.Empty,
            Severity = dto.Severity ?? string.Empty,
            Title = dto.Title ?? string.Empty,
            Description = dto.Description ?? string.Empty,
            Address = dto.Address ?? string.Empty,
            Latitude = NormalizeCoordinate(dto.Latitude),
            Longitude = NormalizeCoordinate(dto.Longitude),
            Confirmations = dto.Confirmations,
            IsResolved = dto.IsResolved,
            CreatedAt = dto.CreatedAt
        };
    }

    private static LeaderboardEntry MapLeaderboardEntry(LeaderboardEntryDto dto)
    {
        return new LeaderboardEntry
        {
            Rank = dto.Rank,
            UserId = dto.UserId ?? string.Empty,
            UserDisplayName = dto.UserDisplayName ?? string.Empty,
            Points = dto.Points,
            TrustLevel = dto.TrustLevel
        };
    }

    private static IssueAnalysis MapIssueAnalysis(IssueAnalysisDto dto)
    {
        return new IssueAnalysis
        {
            Category = ResolveCategoryName(dto.Category),
            ConfidenceScore = dto.ConfidenceScore,
            EstimatedSeverity = dto.EstimatedSeverity,
            Keywords = dto.Keywords ?? [],
            SuggestedTags = dto.SuggestedTags ?? [],
            Reasoning = dto.Reasoning ?? string.Empty,
            AnalyzedAt = dto.AnalyzedAt
        };
    }

    // IssuePriority enum (server): Low=0, Medium=1, High=2, Critical=3.
    // Title-Case so the pill text reads correctly; PriorityToColorConverter lowercases internally.
    private static string MapPriority(int priority) => priority switch
    {
        0 => "Low",
        1 => "Medium",
        2 => "High",
        3 => "Critical",
        _ => "Medium"
    };

    private static string MapStatus(int status, int priority)
    {
        if (priority == 3)
        {
            return AppConstants.StatusCritical;
        }

        return status switch
        {
            AppConstants.StatusNewValue => AppConstants.StatusNew,
            AppConstants.StatusInProgressValue => AppConstants.StatusInProgress,
            AppConstants.StatusFixedValue => AppConstants.StatusResolved,
            _ => AppConstants.StatusNew
        };
    }

    private static string ResolveCityName(string? cityId)
    {
        return cityId switch
        {
            AppConstants.DefaultCityId => "Sofia",
            _ => "Community City"
        };
    }

    private static string ExtractCityNameFromAddress(string address)
    {
        var segments = address.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length == 0 ? "Community City" : segments[^1];
    }

    private static double? NormalizeCoordinate(double? coordinate)
    {
        return coordinate is null or 0 ? null : coordinate;
    }

    private static readonly string[] CategoryDisplayNames =
    {
        "Infrastructure",
        "Public Safety",
        "Environmental Health",
        "Parks",
        "Transportation",
        "Utilities",
        "Sanitation",
        "Public Health",
        "Other"
    };

    private static string ResolveCategoryName(JsonElement? category)
    {
        if (category == null)
        {
            return string.Empty;
        }

        return category.Value.ValueKind switch
        {
            JsonValueKind.String => SpaceCategoryName(category.Value.GetString() ?? string.Empty),
            JsonValueKind.Number => CategoryFromIndex(category.Value.GetInt32()),
            _ => string.Empty
        };
    }

    private static string CategoryFromIndex(int index)
    {
        return index >= 0 && index < CategoryDisplayNames.Length
            ? CategoryDisplayNames[index]
            : string.Empty;
    }

    private static string SpaceCategoryName(string pascal)
    {
        if (string.IsNullOrEmpty(pascal))
        {
            return string.Empty;
        }

        var builder = new System.Text.StringBuilder(pascal.Length + 4);
        for (var i = 0; i < pascal.Length; i++)
        {
            if (i > 0 && char.IsUpper(pascal[i]) && !char.IsUpper(pascal[i - 1]))
            {
                builder.Append(' ');
            }
            builder.Append(pascal[i]);
        }
        return builder.ToString();
    }

    private async Task<string> ExtractErrorAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            if (!string.IsNullOrWhiteSpace(content))
            {
                using var doc = JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                    return msg.GetString() ?? Localization.LocalizationService.Get("Common_Error_Generic");
            }
        }
        catch { /* ignore parse errors */ }
        return Localization.LocalizationService.Get("Common_Error_Generic");
    }

    private async Task<ApiEnvelope<T>?> DeserializeEnvelopeAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        return await JsonSerializer.DeserializeAsync<ApiEnvelope<T>>(stream, _jsonOptions, ct);
    }

    private async Task<T?> DeserializeFlexibleAsync<T>(HttpResponseMessage response, CancellationToken ct, params string[] payloadNames)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = document.RootElement;

        if (root.TryGetProperty("data", out var data))
        {
            foreach (var payloadName in payloadNames)
            {
                if (data.TryGetProperty(payloadName, out var nested) && TryDeserializeElement(nested, out T? nestedValue))
                {
                    return nestedValue;
                }
            }

            if (TryDeserializeElement(data, out T? dataValue))
            {
                return dataValue;
            }
        }

        foreach (var payloadName in payloadNames)
        {
            if (root.TryGetProperty(payloadName, out var nested) && TryDeserializeElement(nested, out T? nestedValue))
            {
                return nestedValue;
            }
        }

        if (TryDeserializeElement(root, out T? direct))
        {
            return direct;
        }

        return default;
    }

    private bool TryDeserializeElement<T>(JsonElement element, out T? value)
    {
        value = default;
        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return false;
        }

        if (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(List<>) && element.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        if (!typeof(T).IsGenericType && element.ValueKind == JsonValueKind.Array)
        {
            return false;
        }

        value = JsonSerializer.Deserialize<T>(element.GetRawText(), _jsonOptions);
        return value != null;
    }

    private async Task<string> ExtractApiErrorAsync(HttpResponseMessage response, string fallbackMessage, CancellationToken ct)
    {
        try
        {
            var content = await response.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrWhiteSpace(content))
            {
                return fallbackMessage;
            }

            var envelope = JsonSerializer.Deserialize<ApiEnvelope<object>>(content, _jsonOptions);
            if (!string.IsNullOrWhiteSpace(envelope?.Message))
            {
                return envelope.Message;
            }
        }
        catch (JsonException ex)
        {
            Console.WriteLine($"[AUTH] Parse error: {ex.Message}");
        }

        return fallbackMessage;
    }

    // ---- Extended endpoints added 2026-05-26 for mobile/web parity ----

    public async Task<ApiResult> LikeCommentAsync(string issueId, string commentId, CancellationToken ct = default)
    {
        if (!_connectivity.IsOnline) { return ApiResult.Fail("Offline"); }
        try
        {
            using var response = await _httpClient.PostAsync($"{AppConstants.ApiIssues}/{issueId}/comments/{commentId}/like", null, ct);
            return response.IsSuccessStatusCode ? ApiResult.Ok() : ApiResult.Fail(await ExtractApiErrorAsync(response, "Failed to like comment", ct));
        }
        catch (HttpRequestException ex) { return ApiResult.Fail(ex.Message); }
        catch (TaskCanceledException) { return ApiResult.Fail("Request timed out. Try again."); }
    }

    public async Task<ApiResult> DislikeCommentAsync(string issueId, string commentId, CancellationToken ct = default)
    {
        if (!_connectivity.IsOnline) { return ApiResult.Fail("Offline"); }
        try
        {
            using var response = await _httpClient.PostAsync($"{AppConstants.ApiIssues}/{issueId}/comments/{commentId}/dislike", null, ct);
            return response.IsSuccessStatusCode ? ApiResult.Ok() : ApiResult.Fail(await ExtractApiErrorAsync(response, "Failed to dislike comment", ct));
        }
        catch (HttpRequestException ex) { return ApiResult.Fail(ex.Message); }
        catch (TaskCanceledException) { return ApiResult.Fail("Request timed out. Try again."); }
    }

    public async Task<DraftSuggestion?> GetDraftSuggestionsAsync(string title, string description, CancellationToken ct = default)
    {
        if (!_connectivity.IsOnline) { return null; }
        try
        {
            var body = new { title, description };
            using var response = await _httpClient.PostAsJsonAsync("/api/analysis/issue-draft-suggestions", body, _jsonOptions, ct);
            if (!response.IsSuccessStatusCode) { return null; }
            return await DeserializeFlexibleAsync<DraftSuggestion>(response, ct, "data", "suggestion");
        }
        catch { return null; }
    }

    public async Task<HazardClusterInsight?> GetHazardClusterInsightAsync(IEnumerable<string> hazardIds, CancellationToken ct = default)
    {
        if (!_connectivity.IsOnline) { return null; }
        try
        {
            var body = new { hazardIds = hazardIds.ToList() };
            using var response = await _httpClient.PostAsJsonAsync("/api/safety/insights/cluster", body, _jsonOptions, ct);
            if (!response.IsSuccessStatusCode) { return null; }
            return await DeserializeFlexibleAsync<HazardClusterInsight>(response, ct, "data", "insight");
        }
        catch { return null; }
    }

    public async IAsyncEnumerable<string> StreamIssueSummaryAsync(
        string issueId,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (!_connectivity.IsOnline) { yield break; }
        var url = $"/api/suggestions/issues/{Uri.EscapeDataString(issueId)}/summary/stream";

        HttpResponseMessage? response = null;
        Stream? stream = null;
        StreamReader? reader = null;
        try
        {
            response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode) { yield break; }
            stream = await response.Content.ReadAsStreamAsync(ct);
            reader = new StreamReader(stream);
        }
        catch
        {
            response?.Dispose();
            yield break;
        }

        using (response)
        using (stream)
        using (reader)
        {
            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                string? line;
                try { line = await reader.ReadLineAsync(ct); }
                catch { yield break; }
                if (string.IsNullOrWhiteSpace(line)) { continue; }
                yield return line;
            }
        }
    }

    public async Task<ApiResult> ToggleAnonymousReportingAsync(bool enabled, CancellationToken ct = default)
    {
        if (!_connectivity.IsOnline) { return ApiResult.Fail("Offline"); }
        try
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/safety/anonymous-reporting/toggle", new { enabled }, _jsonOptions, ct);
            return response.IsSuccessStatusCode ? ApiResult.Ok() : ApiResult.Fail(await ExtractApiErrorAsync(response, "Failed to toggle anonymous reporting", ct));
        }
        catch (HttpRequestException ex) { return ApiResult.Fail(ex.Message); }
        catch (TaskCanceledException) { return ApiResult.Fail("Request timed out. Try again."); }
    }

    public async Task<AlertPreferences?> GetAlertPreferencesAsync(CancellationToken ct = default)
    {
        if (!_connectivity.IsOnline) { return null; }
        try
        {
            using var response = await _httpClient.GetAsync("/api/safety/alert-preferences", ct);
            if (!response.IsSuccessStatusCode) { return null; }
            return await DeserializeFlexibleAsync<AlertPreferences>(response, ct, "data", "preferences");
        }
        catch { return null; }
    }

    public async Task<ApiResult> SaveAlertPreferencesAsync(AlertPreferences preferences, CancellationToken ct = default)
    {
        if (!_connectivity.IsOnline) { return ApiResult.Fail("Offline"); }
        try
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/safety/alert-preferences", preferences, _jsonOptions, ct);
            return response.IsSuccessStatusCode ? ApiResult.Ok() : ApiResult.Fail(await ExtractApiErrorAsync(response, "Failed to save alert preferences", ct));
        }
        catch (HttpRequestException ex) { return ApiResult.Fail(ex.Message); }
        catch (TaskCanceledException) { return ApiResult.Fail("Request timed out. Try again."); }
    }

    public async Task<ApiResult> SetProfileVisibilityAsync(string visibility, CancellationToken ct = default)
    {
        if (!_connectivity.IsOnline) { return ApiResult.Fail("Offline"); }
        try
        {
            using var response = await _httpClient.PostAsJsonAsync("/api/users/profile-visibility", new { visibility }, _jsonOptions, ct);
            return response.IsSuccessStatusCode ? ApiResult.Ok() : ApiResult.Fail(await ExtractApiErrorAsync(response, "Failed to update profile visibility", ct));
        }
        catch (HttpRequestException ex) { return ApiResult.Fail(ex.Message); }
        catch (TaskCanceledException) { return ApiResult.Fail("Request timed out. Try again."); }
    }

    public async Task<ReverseGeocodeResult?> ReverseGeocodeAsync(double latitude, double longitude, CancellationToken ct = default)
    {
        if (!_connectivity.IsOnline) { return null; }
        try
        {
            using var response = await _httpClient.GetAsync($"/api/geocoding/reverse?latitude={latitude}&longitude={longitude}", ct);
            if (!response.IsSuccessStatusCode) { return null; }
            return await DeserializeFlexibleAsync<ReverseGeocodeResult>(response, ct, "data");
        }
        catch { return null; }
    }

    public async Task<List<Tag>> GetPopularTagsAsync(int limit = 20, CancellationToken ct = default)
    {
        if (!_connectivity.IsOnline) { return []; }
        try
        {
            using var response = await _httpClient.GetAsync($"/api/tags/popular?limit={limit}", ct);
            if (!response.IsSuccessStatusCode) { return []; }
            var tags = await DeserializeFlexibleAsync<List<Tag>>(response, ct, "data", "tags", "items");
            return tags ?? [];
        }
        catch { return []; }
    }

    public Task<List<Issue>> GetIssuesByTagAsync(string tagName, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        return GetIssuesAsync(filter: null, search: $"#{tagName}", page: page, pageSize: pageSize, ct: ct);
    }

    public async Task<ApiResult> UpdateIssueAsync(string issueId, string title, string description, string address, CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.PutAsJsonAsync(
                $"{AppConstants.ApiIssues}/{Uri.EscapeDataString(issueId)}",
                new { title, description, address }, _jsonOptions, ct);
            return response.IsSuccessStatusCode
                ? new ApiResult(true)
                : new ApiResult(false, await ExtractErrorAsync(response, ct));
        }
        catch (Exception ex) { Console.WriteLine($"[API] Update error: {ex.Message}"); return new ApiResult(false, ex.Message); }
    }

    public async Task<ApiResult> ForgotPasswordAsync(string email, CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                $"{AppConstants.ApiAuth}/forgot-password", new { email }, _jsonOptions, ct);
            return new ApiResult(true);
        }
        catch (Exception ex) { Console.WriteLine($"[API] ForgotPassword error: {ex.Message}"); return new ApiResult(false, ex.Message); }
    }

    public async Task<List<CityInfo>> GetCitiesAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("api/cities", ct);
            if (!response.IsSuccessStatusCode) return [];
            var envelope = await DeserializeEnvelopeAsync<List<CityInfoDto>>(response, ct);
            return envelope?.Data?.Select(d => new CityInfo
            {
                Id = d.Id ?? string.Empty,
                Name = d.Name ?? string.Empty,
                Country = d.Country ?? string.Empty,
                Description = d.Description,
                ImageUrl = d.ImageUrl,
                IssueCount = d.IssueCount
            }).ToList() ?? [];
        }
        catch { return []; }
    }

    public async Task<ApiResult> ReportHazardAsync(string type, string severity, string title, string description, double latitude, double longitude, CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                $"{AppConstants.ApiSafety}/hazards",
                new { type, severity, title, description, latitude, longitude }, _jsonOptions, ct);
            return response.IsSuccessStatusCode
                ? new ApiResult(true)
                : new ApiResult(false, await ExtractErrorAsync(response, ct));
        }
        catch (Exception ex) { return new ApiResult(false, ex.Message); }
    }

    public async Task<EmailPreferences?> GetEmailPreferencesAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("api/users/email-preferences", ct);
            if (!response.IsSuccessStatusCode) return new EmailPreferences();
            var envelope = await DeserializeEnvelopeAsync<EmailPreferences>(response, ct);
            return envelope?.Data ?? new EmailPreferences();
        }
        catch { return new EmailPreferences(); }
    }

    public async Task<ApiResult> SaveEmailPreferencesAsync(EmailPreferences preferences, CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "api/users/email-preferences", preferences, _jsonOptions, ct);
            return response.IsSuccessStatusCode ? new ApiResult(true) : new ApiResult(false);
        }
        catch (Exception ex) { return new ApiResult(false, ex.Message); }
    }

    public async Task<string?> GetCityPreferenceAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("api/users/city-preference", ct);
            if (!response.IsSuccessStatusCode) return null;
            var envelope = await DeserializeEnvelopeAsync<CityPreferenceDto>(response, ct);
            return envelope?.Data?.CityId;
        }
        catch { return null; }
    }

    public async Task<ApiResult> SaveCityPreferenceAsync(string cityId, CancellationToken ct = default)
    {
        try
        {
            using var response = await _httpClient.PostAsJsonAsync(
                "api/users/city-preference", new { cityId }, _jsonOptions, ct);
            return response.IsSuccessStatusCode ? new ApiResult(true) : new ApiResult(false);
        }
        catch (Exception ex) { return new ApiResult(false, ex.Message); }
    }

    private sealed class CityInfoDto
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
        public string? Country { get; set; }
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int IssueCount { get; set; }
    }

    private sealed class CityPreferenceDto
    {
        public string? CityId { get; set; }
    }

    private class IssueSummaryDto
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? CityId { get; set; }
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int Status { get; set; }
        public int Priority { get; set; }
        public JsonElement? Category { get; set; }
        public string? PhotoUrl { get; set; }
        public string? ImageUrl { get; set; }
        public string? MediaUrl { get; set; }
        public int Upvotes { get; set; }
        public int Downvotes { get; set; }
        public int? VoteScore { get; set; }
        public int UserVote { get; set; }
        public DateTime CreatedAt { get; set; }
        public ReporterDto? Reporter { get; set; }
    }

    private sealed class IssueDetailDto : IssueSummaryDto
    {
    }

    private sealed class ReporterDto
    {
        public string? Id { get; set; }
        public string? DisplayName { get; set; }
    }

    private sealed class UserInfoDto
    {
        // JsonElement (not string) so a server that serializes the id as an ObjectId
        // object instead of a string doesn't throw and abort the whole user parse.
        public JsonElement? Id { get; set; }
        public string? Email { get; set; }
        public string? DisplayName { get; set; }
        public int? ReputationScore { get; set; }
        public int? TrustLevel { get; set; }
    }

    private sealed class CommentDto
    {
        public string? Id { get; set; }
        public string? IssueId { get; set; }
        public ReporterDto? Author { get; set; }
        public string? Text { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private sealed class HazardDto
    {
        public string? Id { get; set; }
        public string? Type { get; set; }
        public string? Severity { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public int Confirmations { get; set; }
        public bool IsResolved { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    private sealed class LeaderboardEntryDto
    {
        public int Rank { get; set; }
        public string? UserId { get; set; }
        public string? UserDisplayName { get; set; }
        public int Points { get; set; }
        public int TrustLevel { get; set; }
    }

    private sealed class IssueAnalysisDto
    {
        public JsonElement? Category { get; set; }
        public int ConfidenceScore { get; set; }
        public int EstimatedSeverity { get; set; }
        public List<string>? Keywords { get; set; }
        public List<string>? SuggestedTags { get; set; }
        public string? Reasoning { get; set; }
        public DateTime AnalyzedAt { get; set; }
    }
}
