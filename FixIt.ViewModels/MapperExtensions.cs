using FixIt.Models.Issues;
using FixIt.Models.Common;
using FixIt.Data.Repository.Contracts;
using FixIt.ViewModels.Issues;
using FixIt.ViewModels.Tags;

namespace FixIt.ViewModels;

/// <summary>
/// Mapper extension methods for converting domain models to view models
/// </summary>
public static class MapperExtensions
{
    public static IssueSummaryResponse ToSummaryResponse(this Issue issue) =>
        new()
        {
            Id = issue.Id,
            Title = issue.Title,
            CityId = issue.CityId,
            Status = issue.Status,
            Priority = issue.Priority,
            Upvotes = issue.Upvotes,
            Downvotes = issue.Downvotes,
            CommentCount = issue.CommentCount,
            ViewCount = issue.ViewCount,
            Reporter = issue.Reporter.ToResponse(),
            TagIds = issue.TagIds,
            CreatedAt = issue.CreatedAt,
            LastActivityAt = issue.LastActivityAt
        };

    public static IssueDetailResponse ToDetailResponse(this Issue issue)
    {
        var response = new IssueDetailResponse
        {
            Id = issue.Id,
            Title = issue.Title,
            Description = issue.Description,
            CityId = issue.CityId,
            Address = issue.Address,
            Longitude = 0,  // Default values - these should be extracted from Location in services
            Latitude = 0,
            Status = issue.Status,
            Priority = issue.Priority,
            Upvotes = issue.Upvotes,
            Downvotes = issue.Downvotes,
            CommentCount = issue.CommentCount,
            ViewCount = issue.ViewCount,
            Reporter = issue.Reporter.ToResponse(),
            TagIds = issue.TagIds,
            StatusHistory = issue.StatusHistory.Select(sh => new IssueStatusHistoryResponse
            {
                From = sh.From,
                To = sh.To,
                ChangedByUserId = sh.ChangedByUserId,
                Comment = sh.Comment,
                ChangedAt = sh.ChangedAt
            }),
            MediaIds = issue.MediaIds,
            IsPinned = issue.IsPinned,
            IsLocked = issue.IsLocked,
            CreatedAt = issue.CreatedAt,
            UpdatedAt = issue.UpdatedAt,
            LastActivityAt = issue.LastActivityAt
        };

        return response;
    }

    public static UserSummaryResponse ToResponse(this UserSummary user) =>
        new()
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            AvatarUrl = user.AvatarUrl
        };

    public static TagResponse ToResponse(this Tag tag) =>
        new()
        {
            Id = tag.Id,
            Name = tag.Name,
            Category = tag.Category,
            Description = tag.Description,
            UsageCount = tag.UsageCount,
            IsApproved = tag.IsApproved,
            Aliases = tag.Aliases,
            CreatedAt = tag.CreatedAt,
            UpdatedAt = tag.UpdatedAt
        };

    public static PaginatedResponse<IssueSummaryResponse> ToPaginatedResponse(
        this PagedResult<Issue> result,
        int page,
        int pageSize) =>
        new()
        {
            Items = result.Items.Select(i => i.ToSummaryResponse()),
            Total = result.Total,
            Page = page,
            PageSize = pageSize
        };

    public static PaginatedResponse<TagResponse> ToPaginatedTagResponse(
        this PagedResult<Tag> result,
        int page,
        int pageSize) =>
        new()
        {
            Items = result.Items.Select(t => t.ToResponse()),
            Total = result.Total,
            Page = page,
            PageSize = pageSize
        };
}
