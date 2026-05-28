using FixIt.Data.Repository.Contracts;
using FixIt.Models.AI;
using FixIt.Models.Issues;
using FixIt.Models.Common;
using FixIt.Models.Enums;
using FixIt.Models.Engagement;
using FixIt.Models.Users;
using FixIt.Services.Constants;
using FixIt.Services.Contracts;
using FixIt.Services.Gamification;
using FixIt.Services.Background;
using MongoDB.Bson;
using MongoDB.Driver.GeoJsonObjectModel;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace FixIt.Services;

public class IssueService : IIssueService
{
    private readonly IRepository<Issue> _issueRepo;
    private readonly IRepository<Tag> _tagRepo;
    private readonly IRepository<Vote> _voteRepo;
    private readonly IRepository<ViewEvent> _viewEventRepo;
    private readonly IRepository<Comment> _commentRepo;
    private readonly IReputationService _reputationService;
    private readonly IIssueAnalysisQueue _issueAnalysisQueue;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<IssueService> _logger;

    public IssueService(
        IRepository<Issue> issueRepo, 
        IRepository<Tag> tagRepo,
        IRepository<Vote> voteRepo,
        IRepository<ViewEvent> viewEventRepo,
        IRepository<Comment> commentRepo,
        IReputationService reputationService,
        IIssueAnalysisQueue issueAnalysisQueue,
        UserManager<ApplicationUser> userManager,
        ILogger<IssueService> logger)
    {
        _issueRepo = issueRepo;
        _tagRepo = tagRepo;
        _voteRepo = voteRepo;
        _viewEventRepo = viewEventRepo;
        _commentRepo = commentRepo;
        _reputationService = reputationService;
        _issueAnalysisQueue = issueAnalysisQueue;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<Issue> CreateIssueAsync(
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
        string? address = null)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException(ValidationMessages.IssuesTitleRequired, nameof(title));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException(ValidationMessages.IssuesDescriptionRequired, nameof(description));
        if (title.Length > 200)
            throw new ArgumentException(ValidationMessages.IssuesTitleTooLong, nameof(title));
        if (description.Length > 5000)
            throw new ArgumentException(ValidationMessages.IssuesDescriptionTooLong, nameof(description));

        var issue = new Issue
        {
            Title = title.Trim(),
            Description = description.Trim(),
            Location = GeoJson.Point(GeoJson.Geographic(longitude, latitude)),
            Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
            CityId = cityId,
            Reporter = reporter,
            IsAnonymous = isAnonymous,
            Status = IssueStatus.New,
            Priority = priority ?? IssuePriority.Medium,
            Category = category,
            Department = string.IsNullOrWhiteSpace(department) ? null : department.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow,
            Upvotes = 1, // Creator's initial upvote
            Downvotes = 0
        };

        // Handle tags if provided
        if (tagNames != null && tagNames.Any())
        {
            foreach (var tagName in tagNames)
            {
                if (string.IsNullOrWhiteSpace(tagName))
                    continue;

                var normalized = tagName.Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(normalized))
                    continue;

                var tags = await _tagRepo.FindAsync(t => t.Name == normalized);
                var tag = tags.FirstOrDefault();

                if (tag == null)
                {
                    // Create missing tag automatically and map to issue
                    tag = await _tagRepo.InsertAsync(new FixIt.Models.Issues.Tag
                    {
                        Name = normalized,
                        IsApproved = true,
                        UsageCount = 0,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
                }

                if (tag != null)
                {
                    issue.TagIds.Add(tag.Id);
                }
            }
        }

        var createdIssue = await _issueRepo.InsertAsync(issue);
        
        // Record creator's vote
        await _voteRepo.InsertAsync(new Vote
        {
            IssueId = createdIssue.Id,
            UserId = reporter.Id,
            Value = VoteType.Up,
            CreatedAt = DateTime.UtcNow
        });

        // Award reputation points to the reporter (only if not anonymous)
        if (!isAnonymous)
        {
            await _reputationService.AddPointsAsync(
                reporter.Id,
                5,
                "issue_reported",
                issueId: createdIssue.Id);
        }

        // Schedule AI analysis through background queue so failures are observable and request-safe.
        try
        {
            await _issueAnalysisQueue.QueueAnalysisAsync(createdIssue.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unable to enqueue analysis for issue {IssueId}", createdIssue.Id);
        }

        return createdIssue;
    }

    public async Task<Issue?> GetIssueByIdAsync(string issueId)
    {
        var issue = await _issueRepo.GetByIdAsync(issueId);
        if (issue != null && !issue.IsDeleted)
        {
            // Note: View count increment is now handled through TrackViewAsync
            // to prevent duplicate counting. This method does not increment views anymore.
        }
        return issue?.IsDeleted == false ? issue : null;
    }

    /// <summary>
    /// Track a view event for an issue. Only increments the issue's view count if this is a new view
    /// from a unique user/session within the tracking window.
    /// </summary>
    /// <param name="issueId">The issue ID</param>
    /// <param name="userId">The user ID (can be null for anonymous users)</param>
    /// <param name="sessionId">Session ID for tracking anonymous users</param>
    /// <param name="ipAddress">IP address for additional tracking</param>
    /// <returns>True if view was recorded, false if it was a duplicate</returns>
    public async Task<bool> TrackViewAsync(string issueId, string? userId = null, string? sessionId = null, string? ipAddress = null)
    {
        var issue = await _issueRepo.GetByIdAsync(issueId);
        if (issue == null || issue.IsDeleted)
            return false;

        // Check if this user/session has viewed this issue in the last 24 hours
        var cutoffTime = DateTime.UtcNow.AddHours(-24);
        
        var existingViews = await _viewEventRepo.FindAsync(v => 
            v.IssueId == issueId && 
            v.ViewedAt > cutoffTime &&
            (
                (userId != null && v.UserId == userId) ||
                (userId == null && sessionId != null && v.SessionId == sessionId) ||
                (userId == null && sessionId == null && v.IpAddress == ipAddress)
            )
        );

        // If a view from this user/session exists in the tracking window, don't count it
        if (existingViews.Any())
            return false;

        // Record the view event
        var viewEvent = new ViewEvent
        {
            IssueId = issueId,
            UserId = userId,
            SessionId = sessionId,
            IpAddress = ipAddress,
            ViewedAt = DateTime.UtcNow
        };

        await _viewEventRepo.InsertAsync(viewEvent);

        // Increment the view count
        issue.ViewCount++;
        issue.LastActivityAt = DateTime.UtcNow;
        await _issueRepo.ReplaceAsync(issueId, issue);

        return true;
    }

    public async Task<PagedResult<Issue>> GetIssuesByCityAsync(
        string cityId,
        IssueStatus? status = null,
        int page = 1,
        int pageSize = 20)
    {
        ValidatePagination(ref page, ref pageSize);
        var skip = (page - 1) * pageSize;

        if (status.HasValue)
        {
            return await _issueRepo.QueryAsync(
                i => i.CityId == cityId && i.Status == status.Value && !i.IsDeleted,
                skip,
                pageSize
            );
        }

        return await _issueRepo.QueryAsync(
            i => i.CityId == cityId && !i.IsDeleted,
            skip,
            pageSize
        );
    }

    public async Task<PagedResult<Issue>> SearchIssuesAsync(
        string cityId,
        string? searchQuery = null,
        IEnumerable<string>? tagIds = null,
        IssueStatus? status = null,
        IssuePriority? priority = null,
        int page = 1,
        int pageSize = 20)
    {
        ValidatePagination(ref page, ref pageSize);
        var skip = (page - 1) * pageSize;

        // FIXME: Hand-rolled Expression.AndAlso/Invoke composition is brittle (any
        // rename of an Issue property silently breaks the MongoDB translation at
        // runtime). Migrate to LinqKit.PredicateBuilder once the dependency budget
        // allows it; the multi-filter combinations are exercised by integration
        // tests under FixIt.Tests.Services.IssueServiceTests.
        var filters = new List<System.Linq.Expressions.Expression<System.Func<Issue, bool>>>
        {
            i => i.CityId == cityId && !i.IsDeleted
        };

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var lowerQuery = searchQuery.ToLowerInvariant();
            filters.Add(i => i.Title.ToLower().Contains(lowerQuery) || i.Description.ToLower().Contains(lowerQuery));
        }

        if (status.HasValue)
        {
            filters.Add(i => i.Status == status.Value);
        }

        if (priority.HasValue)
        {
            filters.Add(i => i.Priority == priority.Value);
        }

        if (tagIds?.Any() == true)
        {
            var tagIdList = tagIds.ToList();
            filters.Add(i => i.TagIds.Any(t => tagIdList.Contains(t)));
        }

        // Combine filters with AND logic
        System.Linq.Expressions.Expression<System.Func<Issue, bool>> combinedFilter = filters[0];
        foreach (var filter in filters.Skip(1))
        {
            var parameter = System.Linq.Expressions.Expression.Parameter(typeof(Issue));
            var combined = System.Linq.Expressions.Expression.AndAlso(
                System.Linq.Expressions.Expression.Invoke(combinedFilter, parameter),
                System.Linq.Expressions.Expression.Invoke(filter, parameter)
            );
            combinedFilter = System.Linq.Expressions.Expression.Lambda<System.Func<Issue, bool>>(combined, parameter);
        }

        return await _issueRepo.QueryAsync(combinedFilter, skip, pageSize);
    }

    public async Task<PagedResult<Issue>> GetUserIssuesAsync(string userId, int page = 1, int pageSize = 20)
    {
        ValidatePagination(ref page, ref pageSize);
        var skip = (page - 1) * pageSize;
        
        return await _issueRepo.QueryAsync(
            i => i.Reporter.Id == userId && !i.IsDeleted,
            skip,
            pageSize
        );
    }

    public async Task UpdateIssueStatusAsync(
        string issueId,
        IssueStatus newStatus,
        string changedByUserId,
        string? comment = null)
    {
        var issue = await _issueRepo.GetByIdAsync(issueId);
        if (issue == null)
            throw new InvalidOperationException("Issue not found");

        if (issue.IsDeleted)
            throw new InvalidOperationException("Cannot modify a deleted issue");

        // Validate status transition
        if (!IsValidStatusTransition(issue.Status, newStatus))
            throw new InvalidOperationException($"Cannot transition from {issue.Status} to {newStatus}");

        if (issue.IsLocked && newStatus != IssueStatus.Archived)
            throw new InvalidOperationException("Cannot modify a locked issue except to archive it");

        issue.StatusHistory.Add(new IssueStatusHistory
        {
            From = issue.Status,
            To = newStatus,
            ChangedByUserId = changedByUserId,
            Comment = comment,
            ChangedAt = DateTime.UtcNow
        });

        var oldStatus = issue.Status;
        issue.Status = newStatus;
        issue.UpdatedAt = DateTime.UtcNow;
        issue.LastActivityAt = DateTime.UtcNow;

        await _issueRepo.ReplaceAsync(issueId, issue);

        // Award reputation points to the issue reporter based on status changes
        if (!string.IsNullOrEmpty(issue.Reporter?.Id))
        {
            // Reporter gets 5 points when issue is confirmed (only once)
            if (newStatus == IssueStatus.Confirmed && oldStatus != IssueStatus.Confirmed)
            {
                await _reputationService.AddPointsAsync(
                    issue.Reporter.Id,
                    5,
                    "issue_confirmed",
                    issueId: issueId);
            }
            
            // Reporter gets 10 additional points when issue is fixed
            if (newStatus == IssueStatus.Fixed && oldStatus != IssueStatus.Fixed)
            {
                await _reputationService.AddPointsAsync(
                    issue.Reporter.Id,
                    10,
                    "issue_resolved",
                    issueId: issueId);
            }
        }
    }

    public async Task UpdateIssuePriorityAsync(string issueId, IssuePriority priority)
    {
        var issue = await _issueRepo.GetByIdAsync(issueId);
        if (issue == null)
            throw new InvalidOperationException("Issue not found");

        if (issue.IsDeleted)
            throw new InvalidOperationException("Cannot modify a deleted issue");

        issue.Priority = priority;
        issue.UpdatedAt = DateTime.UtcNow;
        await _issueRepo.ReplaceAsync(issueId, issue);
    }

    public async Task AddVoteAsync(string issueId, string userId, VoteType voteType)
    {
        var issue = await _issueRepo.GetByIdAsync(issueId);
        if (issue == null)
            throw new InvalidOperationException("Issue not found");

        // Check if user already voted
        var existingVotes = await _voteRepo.FindAsync(v => v.IssueId == issueId && v.UserId == userId);
        var existingVote = existingVotes.FirstOrDefault();

        if (existingVote != null)
        {
            // Remove old vote from count
            if (existingVote.Value == VoteType.Up)
                issue.Upvotes = Math.Max(0, issue.Upvotes - 1);
            else
                issue.Downvotes = Math.Max(0, issue.Downvotes - 1);

            // Delete old vote
            await _voteRepo.DeleteAsync(existingVote.Id);
        }

        // Add new vote to count
        if (voteType == VoteType.Up)
            issue.Upvotes++;
        else
            issue.Downvotes++;

        issue.LastActivityAt = DateTime.UtcNow;
        await _issueRepo.ReplaceAsync(issueId, issue);

        // Record the vote
        await _voteRepo.InsertAsync(new Vote
        {
            IssueId = issueId,
            UserId = userId,
            Value = voteType,
            CreatedAt = DateTime.UtcNow
        });

        // Award reputation points to the issue reporter when they receive an upvote
        if (voteType == VoteType.Up && issue.Reporter.Id != userId)
        {
            await _reputationService.AddPointsAsync(
                issue.Reporter.Id,
                2,
                "received_upvote",
                issueId: issueId);
        }
    }

    public async Task RemoveVoteAsync(string issueId, string userId)
    {
        var issue = await _issueRepo.GetByIdAsync(issueId);
        if (issue == null)
            throw new InvalidOperationException("Issue not found");

        var existingVotes = await _voteRepo.FindAsync(v => v.IssueId == issueId && v.UserId == userId);
        var existingVote = existingVotes.FirstOrDefault();

        if (existingVote != null)
        {
            if (existingVote.Value == VoteType.Up)
                issue.Upvotes = Math.Max(0, issue.Upvotes - 1);
            else
                issue.Downvotes = Math.Max(0, issue.Downvotes - 1);

            await _voteRepo.DeleteAsync(existingVote.Id);
            issue.LastActivityAt = DateTime.UtcNow;
            await _issueRepo.ReplaceAsync(issueId, issue);
        }
    }

    public async Task DeleteIssueAsync(string issueId)
    {
        var issue = await _issueRepo.GetByIdAsync(issueId);
        if (issue == null)
            throw new InvalidOperationException("Issue not found");

        issue.IsDeleted = true;
        issue.UpdatedAt = DateTime.UtcNow;

        await _issueRepo.ReplaceAsync(issueId, issue);
    }

    private static bool IsValidStatusTransition(IssueStatus from, IssueStatus to)
    {
        // Define valid transitions
        var validTransitions = new Dictionary<IssueStatus, IssueStatus[]>
        {
            { IssueStatus.New, new[] { IssueStatus.Confirmed, IssueStatus.Rejected, IssueStatus.Duplicate } },
            { IssueStatus.Confirmed, new[] { IssueStatus.InProgress, IssueStatus.Rejected } },
            { IssueStatus.InProgress, new[] { IssueStatus.Fixed, IssueStatus.Rejected } },
            { IssueStatus.Fixed, new[] { IssueStatus.Archived } },
            { IssueStatus.Rejected, new[] { IssueStatus.Confirmed, IssueStatus.Archived } },
            { IssueStatus.Duplicate, new[] { IssueStatus.Archived } },
            { IssueStatus.Archived, Array.Empty<IssueStatus>() }
        };

        return validTransitions.TryGetValue(from, out var validNextStates) && validNextStates.Contains(to);
    }

    private static void ValidatePagination(ref int page, ref int pageSize)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 100) pageSize = 100; // Max page size
    }

    private static IEnumerable<Issue> ApplySort(IEnumerable<Issue> issues, IssueSortOption sort)
    {
        return sort switch
        {
            IssueSortOption.MostVoted => issues
                .OrderByDescending(i => i.Upvotes - i.Downvotes)
                .ThenByDescending(i => i.Upvotes)
                .ThenByDescending(i => i.LastActivityAt)
                .ThenByDescending(i => i.CreatedAt),
            IssueSortOption.MostViewed => issues
                .OrderByDescending(i => i.ViewCount)
                .ThenByDescending(i => i.LastActivityAt)
                .ThenByDescending(i => i.CreatedAt),
            _ => issues
                .OrderByDescending(i => i.LastActivityAt)
                .ThenByDescending(i => i.CreatedAt)
        };
    }

    /// <summary>
    /// Soft delete an issue - marks it as deleted but keeps data for recovery
    /// </summary>
    public async Task SoftDeleteIssueAsync(string issueId)
    {
        var issue = await _issueRepo.GetByIdAsync(issueId);
        if (issue == null)
            throw new KeyNotFoundException($"Issue {issueId} not found");

        issue.IsDeleted = true;
        issue.UpdatedAt = DateTime.UtcNow;

        await _issueRepo.ReplaceAsync(issueId, issue);
    }

    /// <summary>
    /// Restore a soft-deleted issue
    /// </summary>
    public async Task RestoreIssueAsync(string issueId)
    {
        var issue = await _issueRepo.GetByIdAsync(issueId);
        if (issue == null)
            throw new KeyNotFoundException($"Issue {issueId} not found");

        issue.IsDeleted = false;
        issue.UpdatedAt = DateTime.UtcNow;

        await _issueRepo.ReplaceAsync(issueId, issue);
    }

    /// <summary>
    /// Get every non-deleted issue for a city without pagination.
    /// </summary>
    /// <remarks>
    /// Materialises the full result set in memory. Safe for the public overview /
    /// heatmap aggregations where the city's issue volume is bounded, but do NOT
    /// call this from request hot paths or aggregations over large cities — use
    /// the paginated <see cref="GetIssuesByCityAsync(string, IssueStatus?, int, int)"/>
    /// overload instead.
    /// </remarks>
    public async Task<List<Issue>> GetIssuesByCityAsync(string cityId)
    {
        var issues = await _issueRepo.FindAsync(i =>
            i.CityId == cityId && !i.IsDeleted);

        return issues
            .OrderByDescending(i => i.CreatedAt)
            .ToList();
    }

    /// <summary>
    /// Get count of issues for a specific city
    /// </summary>
    public async Task<int> GetIssueCountByCityAsync(string cityId)
    {
        var issues = await _issueRepo.FindAsync(i => 
            i.CityId == cityId && !i.IsDeleted);
        
        return issues.Count();
    }

    /// <summary>
    /// Get all issues with optional filtering and pagination
    /// </summary>
    public async Task<PagedResult<Issue>> GetAllIssuesAsync(
        string? searchQuery = null,
        IssueStatus? status = null,
        IssuePriority? priority = null,
        IssueCategory? category = null,
        DateTime? fromUtc = null,
        DateTime? toUtc = null,
        IssueSortOption sort = IssueSortOption.Newest,
        int page = 1,
        int pageSize = 20)
    {
        ValidatePagination(ref page, ref pageSize);
        var skip = (page - 1) * pageSize;

        // Build filters
        var filters = new List<System.Linq.Expressions.Expression<System.Func<Issue, bool>>>
        {
            i => !i.IsDeleted
        };

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            var lowerQuery = searchQuery.ToLowerInvariant();
            filters.Add(i => i.Title.ToLower().Contains(lowerQuery) || i.Description.ToLower().Contains(lowerQuery));
        }

        if (status.HasValue)
        {
            filters.Add(i => i.Status == status.Value);
        }

        if (priority.HasValue)
        {
            filters.Add(i => i.Priority == priority.Value);
        }

        if (category.HasValue)
        {
            filters.Add(i => i.Category == category.Value);
        }

        if (fromUtc.HasValue)
        {
            var fromDate = fromUtc.Value.Date;
            filters.Add(i => i.CreatedAt >= fromDate);
        }

        if (toUtc.HasValue)
        {
            var toExclusive = toUtc.Value.Date.AddDays(1);
            filters.Add(i => i.CreatedAt < toExclusive);
        }

        // Combine filters
        System.Linq.Expressions.Expression<System.Func<Issue, bool>> combinedFilter = filters[0];
        foreach (var filter in filters.Skip(1))
        {
            var parameter = System.Linq.Expressions.Expression.Parameter(typeof(Issue));
            var combined = System.Linq.Expressions.Expression.AndAlso(
                System.Linq.Expressions.Expression.Invoke(combinedFilter, parameter),
                System.Linq.Expressions.Expression.Invoke(filter, parameter));
            combinedFilter = System.Linq.Expressions.Expression.Lambda<System.Func<Issue, bool>>(
                combined, parameter);
        }

        // Use QueryAsync for paginated results - database handles sorting and pagination
        var results = await _issueRepo.QueryAsync(combinedFilter, skip, pageSize);
        
        // Apply sorting on the paginated results (limited to pageSize items, not all)
        var sortedIssues = ApplySort(results.Items.AsEnumerable(), sort).ToList();

        return new PagedResult<Issue>
        {
            Items = sortedIssues,
            Total = results.Total
        };
    }

    public async Task<IssuePublicOverview> GetPublicIssueOverviewAsync(int featuredCount = 3)
    {
        // Use CountAsync for each status - no need to load all issues into memory
        var totalIssues = await _issueRepo.CountAsync(i => !i.IsDeleted);
        var newIssues = await _issueRepo.CountAsync(i => !i.IsDeleted && i.Status == IssueStatus.New);
        var confirmedIssues = await _issueRepo.CountAsync(i => !i.IsDeleted && i.Status == IssueStatus.Confirmed);
        var inProgressIssues = await _issueRepo.CountAsync(i => !i.IsDeleted && i.Status == IssueStatus.InProgress);
        var fixedIssues = await _issueRepo.CountAsync(i => !i.IsDeleted && i.Status == IssueStatus.Fixed);
        var criticalIssues = await _issueRepo.CountAsync(i => !i.IsDeleted && i.Priority == IssuePriority.Critical);

        // Get featured issues - only load top N items
        var pagedFeaturedIssues = await _issueRepo.QueryAsync(i => !i.IsDeleted, 0, featuredCount);
        var featured = ApplySort(pagedFeaturedIssues.Items.AsEnumerable(), IssueSortOption.MostVoted)
            .ToList();

        // For cities covered, we still need to load some data, but limit to a reasonable amount
        // TODO: Consider adding a CountDistinctAsync method to the repository
        var allIssues = await _issueRepo.FindAsync(i => !i.IsDeleted);
        var citiesCovered = allIssues
            .Where(i => !string.IsNullOrWhiteSpace(i.CityId))
            .Select(i => i.CityId)
            .Distinct(StringComparer.Ordinal)
            .Count();

        return new IssuePublicOverview
        {
            TotalIssues = (int)totalIssues,
            NewIssues = (int)newIssues,
            ConfirmedIssues = (int)confirmedIssues,
            InProgressIssues = (int)inProgressIssues,
            FixedIssues = (int)fixedIssues,
            CriticalIssues = (int)criticalIssues,
            CitiesCovered = citiesCovered,
            FeaturedIssues = featured
        };
    }

    public async Task<PagedResult<Issue>> GetIssuesByTagAsync(
        string tagId,
        int page = 1,
        int pageSize = 20)
    {
        if (string.IsNullOrWhiteSpace(tagId))
        {
            throw new ArgumentException("Tag ID is required", nameof(tagId));
        }

        ValidatePagination(ref page, ref pageSize);
        var skip = (page - 1) * pageSize;

        var filters = new List<System.Linq.Expressions.Expression<System.Func<Issue, bool>>>
        {
            i => !i.IsDeleted && i.TagIds.Contains(tagId)
        };

        System.Linq.Expressions.Expression<System.Func<Issue, bool>> combinedFilter = filters[0];

        var allIssues = await _issueRepo.FindAsync(combinedFilter);
        var totalCount = allIssues.Count();

        var issues = allIssues
            .OrderByDescending(i => i.CreatedAt)
            .Skip(skip)
            .Take(pageSize)
            .ToList();

        return new PagedResult<Issue>
        {
            Items = issues,
            Total = totalCount
        };
    }

    public async Task UpdateIssueAsync(Issue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
            
        issue.UpdatedAt = DateTime.UtcNow;
        await _issueRepo.ReplaceAsync(issue.Id, issue);
    }

    // Comment CRUD + reactions extracted to CommentService (Phase 3 god-class decomposition).
}
