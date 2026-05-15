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
    private readonly IAnalyticsService _analytics;
    private readonly IApiService _api;
    private readonly IAuthService _auth;
    private readonly IPerformanceService _performance;
    private CancellationTokenSource _cts = new();
    private bool _disposed;
    private bool _subscribed;

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
    private string _commentText = string.Empty;

    [ObservableProperty]
    private string _commentError = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isLoadingComments;

    [ObservableProperty]
    private bool _isLoggedIn;

    public ObservableCollection<Comment> Comments { get; } = [];

    public bool HasIssuePhoto => !string.IsNullOrWhiteSpace(Issue?.PhotoUrl);

    public bool HasCommentError => !string.IsNullOrWhiteSpace(CommentError);

    public bool HasComments => Comments.Count > 0;

    public bool HasNoComments => Comments.Count == 0 && !IsLoadingComments;

    partial void OnIssueChanged(Issue? value)
    {
        OnPropertyChanged(nameof(HasIssuePhoto));
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
            await LoadCommentsAsync(_cts.Token);
        }
    }

    public void OnDisappearing()
    {
        CancelAndRenew();
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
                Comments.Clear();
                foreach (var comment in comments)
                {
                    Comments.Add(comment);
                }

                OnPropertyChanged(nameof(HasComments));
                OnPropertyChanged(nameof(HasNoComments));
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
                    Comments.Add(comment);
                    CommentText = string.Empty;
                    OnPropertyChanged(nameof(HasComments));
                    OnPropertyChanged(nameof(HasNoComments));
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
    }
}
