using FixIt.Data.Repository.Contracts;
using FixIt.Models.Issues;
using FixIt.Models.Common;
using FixIt.Models.Enums;
using FixIt.Services.Contracts;
using MongoDB.Driver.GeoJsonObjectModel;

namespace FixIt.Services;

public class IssueService : IIssueService
{
    private readonly IRepository<Issue> _issueRepo;
    private readonly IRepository<Tag> _tagRepo;

    public IssueService(IRepository<Issue> issueRepo, IRepository<Tag> tagRepo)
    {
        _issueRepo = issueRepo;
        _tagRepo = tagRepo;
    }

    public async Task<Issue> CreateIssueAsync(
        string title,
        string description,
        double longitude,
        double latitude,
        string cityId,
        string neighborhoodId,
        UserSummary reporter,
        IEnumerable<string>? tagNames = null)
    {
        var issue = new Issue
        {
            Title = title,
            Description = description,
            Location = GeoJson.Point(GeoJson.Geographic(longitude, latitude)),
            CityId = cityId,
            NeighborhoodId = neighborhoodId,
            Reporter = reporter,
            Status = IssueStatus.New,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            LastActivityAt = DateTime.UtcNow
        };

        // Handle tags if provided
        if (tagNames != null && tagNames.Any())
        {
            foreach (var tagName in tagNames)
            {
                var tags = await _tagRepo.FindAsync(t => t.Name == tagName.ToLowerInvariant());
                var tag = tags.FirstOrDefault();

                if (tag != null)
                {
                    issue.TagIds.Add(tag.Id);
                    // Tag usage count will be incremented via separate TagService method if needed
                }
            }
        }

        return await _issueRepo.InsertAsync(issue);
    }

    public async Task<Issue?> GetIssueByIdAsync(string issueId)
    {
        return await _issueRepo.GetByIdAsync(issueId);
    }

    public async Task<PagedResult<Issue>> GetIssuesByCityAsync(
        string cityId,
        IssueStatus? status = null,
        int page = 1,
        int pageSize = 20)
    {
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

    public async Task<IEnumerable<Issue>> GetUserIssuesAsync(string userId)
    {
        return await _issueRepo.FindAsync(i => i.Reporter.Id == userId && !i.IsDeleted);
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
}