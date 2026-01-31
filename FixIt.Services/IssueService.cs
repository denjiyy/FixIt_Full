using FixIt.Data.Repository.Contracts;
using FixIt.Models.Issues;
using FixIt.Models.Common;
using FixIt.Models.Enums;
using FixIt.Models.Engagement;
using FixIt.Services.Contracts;
using FixIt.Services.Gamification;
using FixIt.Services.AI;
using MongoDB.Driver.GeoJsonObjectModel;

namespace FixIt.Services;

public class IssueService : IIssueService
{
    private readonly IRepository<Issue> _issueRepo;
    private readonly IRepository<Tag> _tagRepo;
    private readonly IRepository<Vote> _voteRepo;
    private readonly IReputationService _reputationService;
    private readonly IIssueAnalysisService _analysisService;

    public IssueService(
        IRepository<Issue> issueRepo, 
        IRepository<Tag> tagRepo,
        IRepository<Vote> voteRepo,
        IReputationService reputationService,
        IIssueAnalysisService analysisService)
    {
        _issueRepo = issueRepo;
        _tagRepo = tagRepo;
        _voteRepo = voteRepo;
        _reputationService = reputationService;
        _analysisService = analysisService;
    }

    public async Task<Issue> CreateIssueAsync(
        string title,
        string description,
        double longitude,
        double latitude,
        string cityId,
        UserSummary reporter,
        IEnumerable<string>? tagNames = null)
    {
        // Validate inputs
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required", nameof(title));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required", nameof(description));
        if (title.Length > 200)
            throw new ArgumentException("Title must be 200 characters or less", nameof(title));
        if (description.Length > 5000)
            throw new ArgumentException("Description must be 5000 characters or less", nameof(description));

        var issue = new Issue
        {
            Title = title.Trim(),
            Description = description.Trim(),
            Location = GeoJson.Point(GeoJson.Geographic(longitude, latitude)),
            CityId = cityId,
            Reporter = reporter,
            Status = IssueStatus.New,
            Priority = IssuePriority.Medium,
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

                var tags = await _tagRepo.FindAsync(t => t.Name == tagName.ToLowerInvariant());
                var tag = tags.FirstOrDefault();

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

        // Award reputation points to the reporter
        await _reputationService.AddPointsAsync(
            reporter.Id,
            5,
            "issue_reported",
            issueId: createdIssue.Id);

        // Analyze issue with AI (asynchronous, don't await to avoid delays)
        _ = Task.Run(async () =>
        {
            try
            {
                await _analysisService.AnalyzeIssueAsync(createdIssue.Id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AI analysis failed: {ex.Message}");
            }
        });

        return createdIssue;
    }

    public async Task<Issue?> GetIssueByIdAsync(string issueId)
    {
        var issue = await _issueRepo.GetByIdAsync(issueId);
        if (issue != null && !issue.IsDeleted)
        {
            // Increment view count
            issue.ViewCount++;
            issue.LastActivityAt = DateTime.UtcNow;
            await _issueRepo.ReplaceAsync(issueId, issue);
        }
        return issue?.IsDeleted == false ? issue : null;
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

        // Build dynamic filter
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

        issue.Status = newStatus;
        issue.UpdatedAt = DateTime.UtcNow;
        issue.LastActivityAt = DateTime.UtcNow;

        await _issueRepo.ReplaceAsync(issueId, issue);

        // Award reputation points to the user who fixed the issue
        if (newStatus == IssueStatus.Fixed)
        {
            await _reputationService.AddPointsAsync(
                changedByUserId,
                10,
                "issue_resolved",
                issueId: issueId);
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
    /// Get all issues for a specific city
    /// </summary>
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

        // Get total count
        var allIssues = await _issueRepo.FindAsync(combinedFilter);
        var totalCount = allIssues.Count();
        var totalPages = Math.Ceiling(totalCount / (double)pageSize);

        // Get paginated results
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
}