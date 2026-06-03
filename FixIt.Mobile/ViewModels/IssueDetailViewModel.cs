using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Localization;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.ViewModels;

[QueryProperty(nameof(IssueId), nameof(IssueId))]
public partial class IssueDetailViewModel : ObservableObject, IQueryAttributable, IDisposable
{
    private const int CommentsPageSize = 20;
    private readonly IAnalyticsService _analytics;
    private readonly IApiService _api;
    private readonly IAuthService _auth;
    private readonly IPerformanceService _performance;
    private CancellationTokenSource _cts = new();
    private CancellationTokenSource? _analysisCts;
    private readonly List<Comment> _allComments = [];
    private int _commentsPage = 1;
    private bool _disposed;
    private bool _subscribed;
    private bool _commentsLoaded;
    private string _commentsLoadedIssueId = string.Empty;

    public IssueDetailViewModel(IApiService api, IAuthService auth, IAnalyticsService analytics, IPerformanceService performance)
    {
        _api = api;
        _auth = auth;
        _analytics = analytics;
        _performance = performance;
        IsLoggedIn = _auth.IsLoggedIn;
        SubscribeAuth();
    }

    [ObservableProperty]
    private string _issueId = string.Empty;

    [ObservableProperty]
    private Issue? _issue;

    [ObservableProperty]
    private HtmlWebViewSource? _issueMapSource;

    [ObservableProperty]
    private IssueAnalysis? _analysis;

    [ObservableProperty]
    private string _commentText = string.Empty;

    [ObservableProperty]
    private string _commentError = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoadingComments;

    [ObservableProperty]
    private bool _isLoadingMoreComments;

    [ObservableProperty]
    private bool _isAnalysisLoading;

    [ObservableProperty]
    private bool _isAnalysisPending;

    [ObservableProperty]
    private bool _isAnalysisUnavailable;

    [ObservableProperty]
    private bool _isLoggedIn;

    public ObservableCollection<Comment> Comments { get; } = [];

    public string[] CommentSortOptions { get; } = ["Newest", "Oldest", "Most Liked"];

    [ObservableProperty]
    private string _selectedCommentSort = "Newest";

    partial void OnSelectedCommentSortChanged(string value)
    {
        SortAndReplaceComments();
    }

    private void SortAndReplaceComments()
    {
        var sorted = SelectedCommentSort switch
        {
            "Oldest" => _allComments.OrderBy(c => c.CreatedAt).ToList(),
            "Most Liked" => _allComments.OrderByDescending(c => c.LikeCount).ToList(),
            _ => _allComments.OrderByDescending(c => c.CreatedAt).ToList()
        };
        _allComments.Clear();
        _allComments.AddRange(sorted);
        _commentsPage = 1;
        ReplaceVisibleComments();
    }

    public ObservableCollection<string> AnalysisKeywords { get; } = [];

    public ObservableCollection<string> SuggestedTags { get; } = [];

    public bool HasIssuePhoto => !string.IsNullOrWhiteSpace(Issue?.PhotoUrl) || (Issue?.MediaUrls.Count > 0);

    public bool HasIssueLocation => Issue?.HasCoordinates == true;

    public ObservableCollection<string> AllMediaUrls { get; } = [];

    public bool HasMediaGallery => AllMediaUrls.Count > 0;

    public bool HasStatusHistory => Issue?.StatusHistory.Count > 0;

    public bool IsIssueOwner => _auth.IsLoggedIn && !string.IsNullOrWhiteSpace(Issue?.AuthorUserId);

    public string LocationText
    {
        get
        {
            if (Issue is not { HasCoordinates: true })
                return string.Empty;

            var coords = $"{Issue.Latitude:F6}, {Issue.Longitude:F6}";
            return string.IsNullOrWhiteSpace(Issue.Address) ? coords : $"{Issue.Address} ({coords})";
        }
    }

    public bool HasAuthorProfile => !string.IsNullOrWhiteSpace(Issue?.AuthorUserId);

    public bool HasCommentError => !string.IsNullOrWhiteSpace(CommentError);

    public bool HasComments => Comments.Count > 0;

    public bool HasNoComments => Comments.Count == 0 && !IsLoadingComments;

    public bool HasMoreComments => _allComments.Count > Comments.Count;

    public bool HasAnalysis => Analysis != null;

    public double AnalysisSeverityProgress => Math.Clamp((Analysis?.EstimatedSeverity ?? 0) / 10d, 0, 1);

    // ── Resolution progress + lifecycle timeline (design "post detail") ──
    private int StatusRank => Issue?.Status?.Trim().ToLowerInvariant() switch
    {
        "resolved" or "fixed" or "closed" or "done" => 4,
        "inprogress" or "in progress" or "in-progress" => 3,
        "confirmed" or "verified" or "triaged" or "acknowledged" => 2,
        "rejected" or "declined" or "duplicate" or "archived" => 1,
        _ => 1
    };

    public double ResolutionProgress => Issue is null ? 0 : Issue.Status?.Trim().ToLowerInvariant() switch
    {
        "resolved" or "fixed" or "closed" or "done" => 1.0,
        "inprogress" or "in progress" or "in-progress" => 0.65,
        "confirmed" or "verified" or "triaged" or "acknowledged" => 0.40,
        "rejected" or "declined" => 0.30,
        "duplicate" or "archived" => 0.20,
        _ => 0.12
    };

    public string ResolutionPercentText => $"{(int)Math.Round(ResolutionProgress * 100)}%";

    public bool StepReportedDone => Issue is not null;
    public bool StepVerifiedDone => Issue is not null && StatusRank >= 2;
    public bool StepCrewDone => Issue is not null && StatusRank >= 3;
    public bool StepResolvedDone => Issue is not null && StatusRank >= 4;

    public string StepReportedSub =>
        string.IsNullOrWhiteSpace(Issue?.AuthorName)
            ? LocalizationService.Get("Detail_Reporter_Fallback")
            : Issue!.AuthorName;

    public string StepVerifiedSub => LocalizationService.Get(StepVerifiedDone ? "Detail_SubConfirmed" : "Detail_SubAwaiting");
    public string StepCrewSub => LocalizationService.Get(StepCrewDone ? "Detail_SubCrew" : "Detail_SubPending");
    public string StepResolvedSub => LocalizationService.Get(StepResolvedDone ? "Detail_SubComplete" : "Detail_SubNotYet");

    partial void OnIssueChanged(Issue? value)
    {
        OnPropertyChanged(nameof(ResolutionProgress));
        OnPropertyChanged(nameof(ResolutionPercentText));
        OnPropertyChanged(nameof(StepReportedDone));
        OnPropertyChanged(nameof(StepVerifiedDone));
        OnPropertyChanged(nameof(StepCrewDone));
        OnPropertyChanged(nameof(StepResolvedDone));
        OnPropertyChanged(nameof(StepReportedSub));
        OnPropertyChanged(nameof(StepVerifiedSub));
        OnPropertyChanged(nameof(StepCrewSub));
        OnPropertyChanged(nameof(StepResolvedSub));

        IssueMapSource = value?.HasCoordinates == true ? MapHtmlBuilder.BuildIssueMap(value) : null;

        AllMediaUrls.Clear();
        if (value != null)
        {
            if (!string.IsNullOrWhiteSpace(value.PhotoUrl))
                AllMediaUrls.Add(value.PhotoUrl);
            foreach (var url in value.MediaUrls)
            {
                if (!string.IsNullOrWhiteSpace(url) && !AllMediaUrls.Contains(url))
                    AllMediaUrls.Add(url);
            }
        }

        OnPropertyChanged(nameof(HasIssuePhoto));
        OnPropertyChanged(nameof(HasIssueLocation));
        OnPropertyChanged(nameof(HasAuthorProfile));
        OnPropertyChanged(nameof(LocationText));
        OnPropertyChanged(nameof(HasMediaGallery));
        OnPropertyChanged(nameof(HasStatusHistory));
        OnPropertyChanged(nameof(IsIssueOwner));
    }

    partial void OnAnalysisChanged(IssueAnalysis? value)
    {
        AnalysisKeywords.Clear();
        SuggestedTags.Clear();
        if (value != null)
        {
            foreach (var keyword in value.Keywords)
            {
                AnalysisKeywords.Add(keyword);
            }

            foreach (var tag in value.SuggestedTags)
            {
                SuggestedTags.Add(tag);
            }
        }

        OnPropertyChanged(nameof(AnalysisSeverityProgress));
        OnPropertyChanged(nameof(HasAnalysis));
    }

    partial void OnIssueIdChanged(string value)
    {
        if (!_commentsLoadedIssueId.Equals(value, StringComparison.Ordinal))
        {
            // FIX B-04: changing issues invalidates the comment-load guard.
            _commentsLoaded = false;
            _commentsLoadedIssueId = string.Empty;
        }
    }

    partial void OnCommentErrorChanged(string value)
    {
        OnPropertyChanged(nameof(HasCommentError));
    }

    partial void OnCommentTextChanged(string value)
    {
        if (HasCommentError)
        {
            CommentError = string.Empty;
        }
    }

    partial void OnIsLoadingCommentsChanged(bool value)
    {
        OnPropertyChanged(nameof(HasNoComments));
    }

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue(nameof(IssueId), out var value))
        {
            IssueId = Uri.UnescapeDataString(value?.ToString() ?? string.Empty);
        }
    }

    public async Task OnAppearingAsync()
    {
        SubscribeAuth();
        await _analytics.TrackScreen("IssueDetail");
        if (!string.IsNullOrWhiteSpace(IssueId))
        {
            await LoadIssueAsync(_cts.Token);
            if (!_commentsLoaded || !_commentsLoadedIssueId.Equals(IssueId, StringComparison.Ordinal))
            {
                await LoadCommentsAsync(_cts.Token);
            }
        }
    }

    public void OnDisappearing()
    {
        CancelAndRenew();
        _analysisCts?.Cancel();
        // FIX B-04: force a fresh comments load after a full page disappearance without duplicating entries.
        _commentsLoaded = false;
        UnsubscribeAuth();
    }

    [RelayCommand]
    private async Task LoadIssueAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(IssueId))
        {
            return;
        }

        try
        {
            IsLoading = true;
            using (_performance.StartTrace("LoadIssueDetail"))
            {
                Issue = await _api.GetIssueAsync(IssueId, ct);
            }

            StartAnalysisPolling();
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "detail_cancelled" });
        }
        catch (Exception ex)
        {
            await _analytics.TrackError(ex);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task LoadCommentsAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(IssueId))
        {
            return;
        }

        try
        {
            IsLoadingComments = true;
            using (_performance.StartTrace("LoadComments"))
            {
                var comments = await _api.GetCommentsAsync(IssueId, ct);
                _allComments.Clear();
                _allComments.AddRange(comments);
                _commentsPage = 1;
                ReplaceVisibleComments();

                _commentsLoaded = true;
                _commentsLoadedIssueId = IssueId;
            }
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "comments_cancelled" });
        }
        catch (Exception ex)
        {
            await _analytics.TrackError(ex);
        }
        finally
        {
            IsLoadingComments = false;
        }
    }

    [RelayCommand]
    private async Task LoadMoreCommentsAsync(CancellationToken ct)
    {
        if (IsLoadingMoreComments || !HasMoreComments)
        {
            return;
        }

        try
        {
            IsLoadingMoreComments = true;
            _commentsPage += 1;
            foreach (var comment in _allComments.Skip(Comments.Count).Take(CommentsPageSize))
            {
                Comments.Add(comment);
            }

            ct.ThrowIfCancellationRequested();
            NotifyCommentState();
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "comments_more_cancelled" });
        }
        catch (Exception ex)
        {
            await _analytics.TrackError(ex);
        }
        finally
        {
            IsLoadingMoreComments = false;
        }
    }

    [RelayCommand]
    private async Task VoteAsync(bool upvote, CancellationToken ct)
    {
        if (Issue == null)
        {
            return;
        }

        HapticService.Click();
        try
        {
            using (_performance.StartTrace("VoteIssue"))
            {
                var result = await _api.VoteAsync(Issue.Id, upvote, ct);
                if (result.Success)
                {
                    Issue.UserHasUpvoted = upvote;
                    Issue.UserHasDownvoted = !upvote;
                    Issue.VoteCount += upvote ? 1 : -1;
                    OnPropertyChanged(nameof(Issue));
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "vote_cancelled" });
        }
        catch (Exception ex)
        {
            await _analytics.TrackError(ex);
        }
    }

    [RelayCommand]
    private void Save()
    {
        if (Issue == null)
        {
            return;
        }

        HapticService.Click();
        Issue.IsSaved = !Issue.IsSaved;
        OnPropertyChanged(nameof(Issue));
    }

    [RelayCommand]
    private async Task ShareAsync()
    {
        if (Issue == null)
        {
            return;
        }

        HapticService.Click();
        try
        {
            await Share.Default.RequestAsync(new ShareTextRequest
            {
                Title = Issue.Title,
                Text = $"{Issue.Title} — {Issue.PrimaryLocation}"
            });
        }
        catch (Exception ex)
        {
            await _analytics.TrackError(ex);
        }
    }

    [RelayCommand]
    private async Task AddCommentAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(IssueId))
        {
            return;
        }

        if (!ValidateComment())
        {
            return;
        }

        HapticService.Click();
        try
        {
            using (_performance.StartTrace("AddComment"))
            {
                var comment = await _api.AddCommentAsync(IssueId, CommentText.Trim(), ct);
                if (comment != null)
                {
                    _allComments.Add(comment);
                    Comments.Add(comment);
                    CommentText = string.Empty;
                    NotifyCommentState();
                }
            }
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "comment_cancelled" });
        }
        catch (Exception ex)
        {
            await _analytics.TrackError(ex);
        }
    }

    private bool ValidateComment()
    {
        CommentError = string.Empty;
        if (string.IsNullOrWhiteSpace(CommentText))
        {
            CommentError = LocalizationService.Get("Detail_Error_CommentRequired");
        }
        else if (CommentText.Length > MobileSettings.MaxCommentLength)
        {
            CommentError = LocalizationService.Get("Detail_Error_CommentLength");
        }

        return !HasCommentError;
    }

    [RelayCommand]
    private async Task LikeCommentAsync(Comment? comment, CancellationToken ct)
    {
        if (comment == null || string.IsNullOrWhiteSpace(IssueId))
            return;
        HapticService.Click();
        var result = await _api.LikeCommentAsync(IssueId, comment.Id, ct);
        if (result.Success)
        {
            comment.LikeCount += comment.UserHasLiked ? -1 : 1;
            if (comment.UserHasDisliked) { comment.DislikeCount--; comment.UserHasDisliked = false; }
            comment.UserHasLiked = !comment.UserHasLiked;
        }
    }

    [RelayCommand]
    private async Task DislikeCommentAsync(Comment? comment, CancellationToken ct)
    {
        if (comment == null || string.IsNullOrWhiteSpace(IssueId))
            return;
        HapticService.Click();
        var result = await _api.DislikeCommentAsync(IssueId, comment.Id, ct);
        if (result.Success)
        {
            comment.DislikeCount += comment.UserHasDisliked ? -1 : 1;
            if (comment.UserHasLiked) { comment.LikeCount--; comment.UserHasLiked = false; }
            comment.UserHasDisliked = !comment.UserHasDisliked;
        }
    }

    [RelayCommand]
    private async Task DeleteIssueAsync(CancellationToken ct)
    {
        if (Issue == null)
            return;

        var confirmed = await Shell.Current.DisplayAlert(
            LocalizationService.Get("Detail_DeleteConfirmTitle"),
            LocalizationService.Get("Detail_DeleteConfirmMessage"),
            LocalizationService.Get("Common_Delete"),
            LocalizationService.Get("Common_Cancel"));

        if (!confirmed)
            return;

        HapticService.Click();
        var result = await _api.DeleteIssueAsync(Issue.Id, ct);
        if (result.Success)
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    [RelayCommand]
    private async Task EditIssueAsync(CancellationToken ct)
    {
        if (Issue == null)
            return;
        HapticService.Click();
        await Shell.Current.GoToAsync($"{AppConstants.RouteEditIssue}?IssueId={Uri.EscapeDataString(Issue.Id)}");
    }

    [RelayCommand]
    private async Task RetryAnalysisAsync(CancellationToken ct)
    {
        HapticService.Click();
        await Task.CompletedTask;
        StartAnalysisPolling();
    }

    [RelayCommand]
    private async Task NavigateToAuthorProfileAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Issue?.AuthorUserId))
        {
            return;
        }

        HapticService.Click();
        await Shell.Current.GoToAsync($"{AppConstants.RoutePublicProfile}?UserId={Uri.EscapeDataString(Issue.AuthorUserId)}");
    }

    private void StartAnalysisPolling()
    {
        _analysisCts?.Cancel();
        _analysisCts?.Dispose();
        _analysisCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
        _ = PollAnalysisAsync(IssueId, _analysisCts.Token);
    }

    private async Task PollAnalysisAsync(string issueId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(issueId))
        {
            return;
        }

        try
        {
            Analysis = null;
            IsAnalysisLoading = true;
            IsAnalysisPending = true;
            IsAnalysisUnavailable = false;

            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            for (var attempt = 0; attempt < 12; attempt++)
            {
                var analysis = await _api.GetAnalysisAsync(issueId, ct);
                if (analysis != null)
                {
                    Analysis = analysis;
                    IsAnalysisPending = false;
                    IsAnalysisUnavailable = false;
                    return;
                }

                if (attempt < 11)
                {
                    await timer.WaitForNextTickAsync(ct);
                }
            }

            IsAnalysisUnavailable = true;
            IsAnalysisPending = false;
        }
        catch (OperationCanceledException ex)
        {
            await _analytics.TrackError(ex, new Dictionary<string, string> { ["reason"] = "analysis_poll_cancelled" });
        }
        catch (Exception ex)
        {
            IsAnalysisUnavailable = true;
            IsAnalysisPending = false;
            await _analytics.TrackError(ex);
        }
        finally
        {
            IsAnalysisLoading = false;
        }
    }

    private void ReplaceVisibleComments()
    {
        Comments.Clear();
        foreach (var comment in _allComments.Take(_commentsPage * CommentsPageSize))
        {
            Comments.Add(comment);
        }

        NotifyCommentState();
    }

    private void NotifyCommentState()
    {
        OnPropertyChanged(nameof(HasComments));
        OnPropertyChanged(nameof(HasNoComments));
        OnPropertyChanged(nameof(HasMoreComments));
    }

    private void OnLoginStateChanged(object? sender, bool isLoggedIn)
    {
        IsLoggedIn = isLoggedIn;
    }

    private void SubscribeAuth()
    {
        if (_subscribed)
        {
            return;
        }

        _auth.LoginStateChanged += OnLoginStateChanged;
        _subscribed = true;
    }

    private void UnsubscribeAuth()
    {
        if (!_subscribed)
        {
            return;
        }

        _auth.LoginStateChanged -= OnLoginStateChanged;
        _subscribed = false;
    }

    private void CancelAndRenew()
    {
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnsubscribeAuth();
        _cts.Cancel();
        _cts.Dispose();
        _analysisCts?.Dispose();
    }
}
