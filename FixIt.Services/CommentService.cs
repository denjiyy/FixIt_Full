using FixIt.Data.Repository.Contracts;
using FixIt.Models.Common;
using FixIt.Models.Engagement;
using FixIt.Models.Issues;
using FixIt.Models.Users;
using FixIt.Services.Constants;
using FixIt.Services.Contracts;
using FixIt.Services.Gamification;
using Microsoft.AspNetCore.Identity;
using MongoDB.Bson;

namespace FixIt.Services;

/// <summary>
/// Comment CRUD and reactions. Extracted from IssueService as a first step in
/// decomposing that 900-line god-class. Talks to the issue repository directly
/// rather than going through IIssueService — comments need to update parent
/// counters, but the dependency cycle gets unpleasant the other way.
/// </summary>
public class CommentService : ICommentService
{
    private readonly IRepository<Comment> _commentRepo;
    private readonly IRepository<Issue> _issueRepo;
    private readonly IReputationService _reputationService;
    private readonly UserManager<ApplicationUser> _userManager;

    public CommentService(
        IRepository<Comment> commentRepo,
        IRepository<Issue> issueRepo,
        IReputationService reputationService,
        UserManager<ApplicationUser> userManager)
    {
        _commentRepo = commentRepo;
        _issueRepo = issueRepo;
        _reputationService = reputationService;
        _userManager = userManager;
    }

    public async Task<Comment> AddCommentAsync(
        string issueId,
        string authorId,
        string text,
        bool isAnonymous = false)
    {
        if (string.IsNullOrWhiteSpace(issueId))
            throw new ArgumentException(ValidationMessages.IssuesIdRequired, nameof(issueId));
        if (string.IsNullOrWhiteSpace(authorId))
            throw new ArgumentException(ValidationMessages.IssuesAuthorIdRequired, nameof(authorId));
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException(ValidationMessages.IssuesCommentTextRequired, nameof(text));
        if (text.Length > 5000)
            throw new ArgumentException(ValidationMessages.IssuesCommentTooLong, nameof(text));

        var issue = await _issueRepo.GetByIdAsync(issueId);
        if (issue == null)
            throw new InvalidOperationException("Issue not found");

        if (issue.IsDeleted)
            throw new InvalidOperationException("Cannot comment on a deleted issue");

        if (issue.IsLocked)
            throw new InvalidOperationException("Cannot comment on a locked issue");

        var comment = new Comment
        {
            IssueId = issueId,
            AuthorId = authorId,
            Text = text.Trim(),
            IsAnonymous = isAnonymous,
            MediaIds = new HashSet<string>(),
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
        };

        var createdComment = await _commentRepo.InsertAsync(comment);

        issue.CommentCount++;
        issue.LastActivityAt = DateTime.UtcNow;
        await _issueRepo.ReplaceAsync(issueId, issue);

        if (!isAnonymous)
        {
            await _reputationService.AddPointsAsync(
                authorId,
                2,
                "comment_posted",
                issueId: issueId,
                commentId: createdComment.Id);
        }

        return createdComment;
    }

    public async Task<List<Comment>> GetCommentsForIssueAsync(string issueId)
    {
        if (string.IsNullOrWhiteSpace(issueId))
            throw new ArgumentException("Issue ID is required", nameof(issueId));

        var comments = await _commentRepo.FindAsync(c =>
            c.IssueId == issueId && !c.IsDeleted);

        var sortedComments = comments
            .OrderByDescending(c => c.CreatedAt)
            .ToList();

        // Batch-load authors in a single query to avoid an N+1 over comments.
        // Carried over from the Phase 1 fix.
        var missingAuthorIds = sortedComments
            .Where(c => !string.IsNullOrEmpty(c.AuthorId) && c.Author == null)
            .Select(c => c.AuthorId!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var authorById = new Dictionary<string, ApplicationUser>(StringComparer.Ordinal);
        if (missingAuthorIds.Count > 0)
        {
            var objectIds = missingAuthorIds
                .Where(id => ObjectId.TryParse(id, out _))
                .Select(ObjectId.Parse)
                .ToList();

            if (objectIds.Count > 0)
            {
                var loaded = _userManager.Users
                    .Where(u => objectIds.Contains(u.Id))
                    .ToList();
                foreach (var u in loaded)
                {
                    authorById[u.Id.ToString()] = u;
                }
            }
        }

        foreach (var comment in sortedComments)
        {
            if (string.IsNullOrEmpty(comment.AuthorId) || comment.Author != null)
                continue;

            if (authorById.TryGetValue(comment.AuthorId, out var user))
            {
                comment.Author = new UserSummary
                {
                    Id = user.Id.ToString(),
                    DisplayName = user.DisplayName ?? user.UserName ?? "Anonymous",
                    AvatarUrl = null,
                };
            }
            else
            {
                comment.Author = new UserSummary
                {
                    Id = comment.AuthorId,
                    DisplayName = "Deleted User",
                    AvatarUrl = null,
                };
            }
        }

        return sortedComments;
    }

    public async Task DeleteCommentAsync(string issueId, string commentId)
    {
        var comment = await _commentRepo.GetByIdAsync(commentId);
        if (comment == null)
            throw new InvalidOperationException("Comment not found");

        if (comment.IssueId != issueId)
            throw new InvalidOperationException("Comment does not belong to this issue");

        comment.IsDeleted = true;
        comment.Text = "[deleted]";

        await _commentRepo.ReplaceAsync(commentId, comment);
    }

    public async Task LikeCommentAsync(string issueId, string commentId, string userId)
    {
        var comment = await _commentRepo.GetByIdAsync(commentId);
        if (comment == null)
            throw new InvalidOperationException("Comment not found");

        if (comment.IssueId != issueId)
            throw new InvalidOperationException("Comment does not belong to this issue");

        comment.DislikedBy?.Remove(userId);
        comment.LikedBy ??= new HashSet<string>();
        comment.LikedBy.Add(userId);

        await _commentRepo.ReplaceAsync(commentId, comment);
    }

    public async Task DislikeCommentAsync(string issueId, string commentId, string userId)
    {
        var comment = await _commentRepo.GetByIdAsync(commentId);
        if (comment == null)
            throw new InvalidOperationException("Comment not found");

        if (comment.IssueId != issueId)
            throw new InvalidOperationException("Comment does not belong to this issue");

        comment.LikedBy?.Remove(userId);
        comment.DislikedBy ??= new HashSet<string>();
        comment.DislikedBy.Add(userId);

        await _commentRepo.ReplaceAsync(commentId, comment);
    }
}
