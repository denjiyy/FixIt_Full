using FixIt.Models.Engagement;

namespace FixIt.Services.Contracts;

/// <summary>
/// Comment CRUD + reactions, extracted from IssueService. Comments are tied to
/// issues (the AddComment path updates the parent issue's counters) but the
/// rest of the comment lifecycle — fetching, soft-delete, likes/dislikes — has
/// no coupling and lives well here on its own.
/// </summary>
public interface ICommentService
{
    Task<Comment> AddCommentAsync(
        string issueId,
        string authorId,
        string text,
        bool isAnonymous = false);

    Task<List<Comment>> GetCommentsForIssueAsync(string issueId);

    Task DeleteCommentAsync(string issueId, string commentId);

    Task LikeCommentAsync(string issueId, string commentId, string userId);

    Task DislikeCommentAsync(string issueId, string commentId, string userId);
}
