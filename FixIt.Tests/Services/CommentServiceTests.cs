using System.Linq.Expressions;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Engagement;
using FixIt.Models.Issues;
using FixIt.Models.Users;
using FixIt.Services;
using FixIt.Services.Gamification;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace FixIt.Tests.Services;

public class CommentServiceTests
{
    private readonly Mock<IRepository<Comment>> _commentRepoMock;
    private readonly Mock<IRepository<Issue>> _issueRepoMock;
    private readonly Mock<IReputationService> _reputationServiceMock;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly CommentService _commentService;

    public CommentServiceTests()
    {
        _commentRepoMock = new Mock<IRepository<Comment>>();
        _issueRepoMock = new Mock<IRepository<Issue>>();
        _reputationServiceMock = new Mock<IReputationService>();

        var userStoreMock = new Mock<IUserStore<ApplicationUser>>();
        _userManagerMock = new Mock<UserManager<ApplicationUser>>(
            userStoreMock.Object,
            It.IsAny<IOptions<IdentityOptions>>(),
            It.IsAny<IPasswordHasher<ApplicationUser>>(),
            It.IsAny<IEnumerable<IUserValidator<ApplicationUser>>>(),
            It.IsAny<IEnumerable<IPasswordValidator<ApplicationUser>>>(),
            It.IsAny<ILookupNormalizer>(),
            It.IsAny<IdentityErrorDescriber>(),
            It.IsAny<IServiceProvider>(),
            It.IsAny<ILogger<UserManager<ApplicationUser>>>());

        _commentService = new CommentService(
            _commentRepoMock.Object,
            _issueRepoMock.Object,
            _reputationServiceMock.Object,
            _userManagerMock.Object);
    }

    [Fact]
    public async Task AddCommentAsync_WithValidComment_CreatesCommentSuccessfully()
    {
        var issue = new Issue { Id = "issue1", Title = "Test", CommentCount = 0 };
        Comment? captured = null;

        _issueRepoMock.Setup(r => r.GetByIdAsync("issue1")).ReturnsAsync(issue);
        _commentRepoMock.Setup(r => r.InsertAsync(It.IsAny<Comment>()))
            .Callback<Comment>(c => captured = c)
            .ReturnsAsync((Comment c) => c);
        _issueRepoMock.Setup(r => r.ReplaceAsync("issue1", It.IsAny<Issue>())).Returns(Task.CompletedTask);
        _reputationServiceMock
            .Setup(r => r.AddPointsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        var result = await _commentService.AddCommentAsync("issue1", "user1", "Great issue!");

        Assert.NotNull(result);
        Assert.Equal("Great issue!", result.Text);
        Assert.Equal(1, issue.CommentCount);
        Assert.Same(result, captured);
    }

    [Fact]
    public async Task AddCommentAsync_OnDeletedIssue_Throws()
    {
        var deletedIssue = new Issue { Id = "issue1", IsDeleted = true };
        _issueRepoMock.Setup(r => r.GetByIdAsync("issue1")).ReturnsAsync(deletedIssue);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _commentService.AddCommentAsync("issue1", "user1", "Hello"));
    }

    [Fact]
    public async Task AddCommentAsync_OnLockedIssue_Throws()
    {
        var lockedIssue = new Issue { Id = "issue1", IsLocked = true };
        _issueRepoMock.Setup(r => r.GetByIdAsync("issue1")).ReturnsAsync(lockedIssue);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _commentService.AddCommentAsync("issue1", "user1", "Hello"));
    }

    [Fact]
    public async Task AddCommentAsync_AnonymousComment_DoesNotAwardReputation()
    {
        var issue = new Issue { Id = "issue1" };
        _issueRepoMock.Setup(r => r.GetByIdAsync("issue1")).ReturnsAsync(issue);
        _commentRepoMock.Setup(r => r.InsertAsync(It.IsAny<Comment>()))
            .ReturnsAsync((Comment c) => c);
        _issueRepoMock.Setup(r => r.ReplaceAsync(It.IsAny<string>(), It.IsAny<Issue>())).Returns(Task.CompletedTask);

        await _commentService.AddCommentAsync("issue1", "user1", "Anonymous!", isAnonymous: true);

        _reputationServiceMock.Verify(
            r => r.AddPointsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task GetCommentsForIssueAsync_ReturnsCommentsForIssue()
    {
        var comments = new List<Comment>
        {
            new() { Id = "c1", IssueId = "issue1", Text = "Comment 1", CreatedAt = DateTime.UtcNow.AddMinutes(-2) },
            new() { Id = "c2", IssueId = "issue1", Text = "Comment 2", CreatedAt = DateTime.UtcNow.AddMinutes(-1) },
        };
        _commentRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Comment, bool>>>()))
            .ReturnsAsync(comments);

        var result = await _commentService.GetCommentsForIssueAsync("issue1");

        Assert.Equal(2, result.Count);
        // Newest first
        Assert.Equal("c2", result[0].Id);
    }

    [Fact]
    public async Task DeleteCommentAsync_SoftDeletesAndReplacesText()
    {
        var comment = new Comment { Id = "c1", IssueId = "issue1", Text = "secret", IsDeleted = false };
        _commentRepoMock.Setup(r => r.GetByIdAsync("c1")).ReturnsAsync(comment);
        _commentRepoMock.Setup(r => r.ReplaceAsync("c1", It.IsAny<Comment>())).Returns(Task.CompletedTask);

        await _commentService.DeleteCommentAsync("issue1", "c1");

        Assert.True(comment.IsDeleted);
        Assert.Equal("[deleted]", comment.Text);
    }

    [Fact]
    public async Task LikeCommentAsync_AddsUserToLikedSetAndRemovesFromDisliked()
    {
        var comment = new Comment
        {
            Id = "c1",
            IssueId = "issue1",
            DislikedBy = new HashSet<string> { "user1" },
        };
        _commentRepoMock.Setup(r => r.GetByIdAsync("c1")).ReturnsAsync(comment);
        _commentRepoMock.Setup(r => r.ReplaceAsync("c1", It.IsAny<Comment>())).Returns(Task.CompletedTask);

        await _commentService.LikeCommentAsync("issue1", "c1", "user1");

        Assert.Contains("user1", comment.LikedBy!);
        Assert.DoesNotContain("user1", comment.DislikedBy);
    }
}
