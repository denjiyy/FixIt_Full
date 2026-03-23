using Xunit;
using Moq;
using FixIt.Services;
using FixIt.Services.Contracts;
using FixIt.Services.Gamification;
using FixIt.Services.AI;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Issues;
using FixIt.Models.Common;
using FixIt.Models.Enums;
using FixIt.Models.Users;
using FixIt.Models.Engagement;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;
using Microsoft.Extensions.Options;

namespace FixIt.Tests.Services;

public class IssueServiceTests
{
    private readonly Mock<IRepository<Issue>> _issueRepoMock;
    private readonly Mock<IRepository<Tag>> _tagRepoMock;
    private readonly Mock<IRepository<Vote>> _voteRepoMock;
    private readonly Mock<IRepository<ViewEvent>> _viewEventRepoMock;
    private readonly Mock<IRepository<Comment>> _commentRepoMock;
    private readonly Mock<IReputationService> _reputationServiceMock;
    private readonly Mock<IIssueAnalysisService> _analysisServiceMock;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly IssueService _issueService;

    public IssueServiceTests()
    {
        _issueRepoMock = new Mock<IRepository<Issue>>();
        _tagRepoMock = new Mock<IRepository<Tag>>();
        _voteRepoMock = new Mock<IRepository<Vote>>();
        _viewEventRepoMock = new Mock<IRepository<ViewEvent>>();
        _commentRepoMock = new Mock<IRepository<Comment>>();
        _reputationServiceMock = new Mock<IReputationService>();
        _analysisServiceMock = new Mock<IIssueAnalysisService>();
        
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

        _issueService = new IssueService(
            _issueRepoMock.Object,
            _tagRepoMock.Object,
            _voteRepoMock.Object,
            _viewEventRepoMock.Object,
            _commentRepoMock.Object,
            _reputationServiceMock.Object,
            _analysisServiceMock.Object,
            _userManagerMock.Object
        );
    }

    [Fact]
    public async Task CreateIssueAsync_WithValidInputs_CreatesIssueSuccessfully()
    {
        // Arrange
        var reporter = new UserSummary { Id = "user1", DisplayName = "Test User" };
        const string title = "Broken Pothole on Main St";
        const string description = "Large pothole near intersection";
        const double longitude = -118.2437;
        const double latitude = 34.0522;
        const string cityId = "city1";

        Issue? capturedIssue = null;
        _issueRepoMock.Setup(r => r.InsertAsync(It.IsAny<Issue>()))
            .Callback<Issue>(i => capturedIssue = i)
            .ReturnsAsync((Issue i) => i);

        _tagRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Tag, bool>>>()))
            .ReturnsAsync(new List<Tag>());

        // Act
        var result = await _issueService.CreateIssueAsync(
            title, description, longitude, latitude, cityId, reporter);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(title.Trim(), result.Title);
        Assert.Equal(description.Trim(), result.Description);
        Assert.Equal(IssueStatus.New, result.Status);
        Assert.Equal(IssuePriority.Medium, result.Priority);
        Assert.Equal(1, result.Upvotes); // Creator's initial upvote
        Assert.False(result.IsAnonymous);
        _issueRepoMock.Verify(r => r.InsertAsync(It.IsAny<Issue>()), Times.Once);
    }

    [Fact]
    public async Task CreateIssueAsync_WithNullTitle_ThrowsArgumentException()
    {
        // Arrange
        var reporter = new UserSummary { Id = "user1", DisplayName = "Test User" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _issueService.CreateIssueAsync(null!, "description", 0, 0, "city1", reporter));
        Assert.Contains("Title is required", ex.Message);
    }

    [Fact]
    public async Task CreateIssueAsync_WithEmptyTitle_ThrowsArgumentException()
    {
        // Arrange
        var reporter = new UserSummary { Id = "user1", DisplayName = "Test User" };

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _issueService.CreateIssueAsync("   ", "description", 0, 0, "city1", reporter));
        Assert.Contains("Title is required", ex.Message);
    }

    [Fact]
    public async Task CreateIssueAsync_WithTitleTooLong_ThrowsArgumentException()
    {
        // Arrange
        var reporter = new UserSummary { Id = "user1", DisplayName = "Test User" };
        var longTitle = new string('a', 201);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _issueService.CreateIssueAsync(longTitle, "description", 0, 0, "city1", reporter));
        Assert.Contains("200 characters or less", ex.Message);
    }

    [Fact]
    public async Task CreateIssueAsync_WithDescriptionTooLong_ThrowsArgumentException()
    {
        // Arrange
        var reporter = new UserSummary { Id = "user1", DisplayName = "Test User" };
        var longDescription = new string('a', 5001);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ArgumentException>(
            () => _issueService.CreateIssueAsync("Title", longDescription, 0, 0, "city1", reporter));
        Assert.Contains("5000 characters or less", ex.Message);
    }

    [Fact]
    public async Task CreateIssueAsync_WithTags_AssignsTagsToIssue()
    {
        // Arrange
        var reporter = new UserSummary { Id = "user1", DisplayName = "Test User" };
        var tag = new Tag { Id = "tag1", Name = "pothole" };
        var tagNames = new[] { "pothole", "repair" };

        var capturedIssue = new Issue();
        _issueRepoMock.Setup(r => r.InsertAsync(It.IsAny<Issue>()))
            .Callback<Issue>(i => capturedIssue = i)
            .ReturnsAsync((Issue i) => i);

        _tagRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Tag, bool>>>()))
            .ReturnsAsync(new List<Tag> { tag });

        // Act
        await _issueService.CreateIssueAsync(
            "Test", "Description", 0, 0, "city1", reporter, tagNames);

        // Assert
        Assert.NotEmpty(capturedIssue.TagIds);
        Assert.Contains("tag1", capturedIssue.TagIds);
    }

    [Fact]
    public async Task CreateIssueAsync_WithUnknownTag_CreatesNewTagAndAssignsToIssue()
    {
        // Arrange
        var reporter = new UserSummary { Id = "user1", DisplayName = "Test User" };
        var tagNames = new[] { "newtag" };

        Issue? capturedIssue = null;
        _issueRepoMock.Setup(r => r.InsertAsync(It.IsAny<Issue>()))
            .Callback<Issue>(i => capturedIssue = i)
            .ReturnsAsync((Issue i) => i);

        _tagRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Tag, bool>>>()))
            .ReturnsAsync(new List<Tag>());

        _tagRepoMock.Setup(r => r.InsertAsync(It.IsAny<Tag>()))
            .ReturnsAsync((Tag t) => t);

        // Act
        await _issueService.CreateIssueAsync("Test", "Description", 0, 0, "city1", reporter, tagNames);

        // Assert
        Assert.NotNull(capturedIssue);
        Assert.NotEmpty(capturedIssue.TagIds);
        _tagRepoMock.Verify(r => r.InsertAsync(It.Is<Tag>(t => t.Name == "newtag")), Times.Once);
    }

    [Fact]
    public async Task CreateIssueAsync_WithAnonymousFlag_CreatesAnonymousIssue()
    {
        // Arrange
        var reporter = new UserSummary { Id = "user1", DisplayName = "Test User" };
        Issue? capturedIssue = null;

        _issueRepoMock.Setup(r => r.InsertAsync(It.IsAny<Issue>()))
            .Callback<Issue>(i => capturedIssue = i)
            .ReturnsAsync((Issue i) => i);

        _tagRepoMock.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<System.Func<Tag, bool>>>()))
            .ReturnsAsync(new List<Tag>());

        // Act
        await _issueService.CreateIssueAsync(
            "Title", "Description", 0, 0, "city1", reporter, null, isAnonymous: true);

        // Assert
        Assert.NotNull(capturedIssue);
        Assert.True(capturedIssue.IsAnonymous);
    }

    [Fact]
    public async Task UpdateIssueStatusAsync_WithValidStatus_UpdatesStatusSuccessfully()
    {
        // Arrange
        var issue = new Issue { Id = "issue1", CityId = "city1", Status = IssueStatus.New, Title = "Test" };
        
        _issueRepoMock.Setup(r => r.GetByIdAsync("issue1"))
            .ReturnsAsync(issue);
        _issueRepoMock.Setup(r => r.ReplaceAsync("issue1", It.IsAny<Issue>()))
            .Returns(Task.CompletedTask);

        // Act
        await _issueService.UpdateIssueStatusAsync("issue1", IssueStatus.InProgress, "user1");

        // Assert
        Assert.Equal(IssueStatus.InProgress, issue.Status);
    }

    [Fact]
    public async Task UpdateIssuePriorityAsync_WithValidPriority_UpdatesPrioritySuccessfully()
    {
        // Arrange
        var issue = new Issue { Id = "issue1", CityId = "city1", Priority = IssuePriority.Medium, Title = "Test" };
        
        _issueRepoMock.Setup(r => r.GetByIdAsync("issue1"))
            .ReturnsAsync(issue);
        _issueRepoMock.Setup(r => r.ReplaceAsync("issue1", It.IsAny<Issue>()))
            .Returns(Task.CompletedTask);

        // Act
        await _issueService.UpdateIssuePriorityAsync("issue1", IssuePriority.Critical);

        // Assert
        Assert.Equal(IssuePriority.Critical, issue.Priority);
    }

    [Fact]
    public async Task TrackViewAsync_WithNewViewer_IncrementViewCount()
    {
        // Arrange
        var issue = new Issue { Id = "issue1", Title = "Test", ViewCount = 0 };
        
        _issueRepoMock.Setup(r => r.GetByIdAsync("issue1"))
            .ReturnsAsync(issue);
        _viewEventRepoMock.Setup(r => r.InsertAsync(It.IsAny<ViewEvent>()))
            .ReturnsAsync((ViewEvent v) => v);
        _issueRepoMock.Setup(r => r.ReplaceAsync("issue1", It.IsAny<Issue>()))
            .Returns(Task.CompletedTask);

        // Act
        await _issueService.TrackViewAsync("issue1", "user1");

        // Assert
        Assert.Equal(1, issue.ViewCount);
    }

    [Fact]
    public async Task AddVoteAsync_WithUpvote_IncrementsUpvotes()
    {
        // Arrange
        var issue = new Issue { Id = "issue1", Title = "Test", Upvotes = 5, Reporter = new UserSummary { Id = "reporter1" } };
        
        _issueRepoMock.Setup(r => r.GetByIdAsync("issue1"))
            .ReturnsAsync(issue);
        _voteRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Vote, bool>>>()))
            .ReturnsAsync(new List<Vote>());
        _voteRepoMock.Setup(r => r.InsertAsync(It.IsAny<Vote>()))
            .ReturnsAsync((Vote v) => v);
        _issueRepoMock.Setup(r => r.ReplaceAsync("issue1", It.IsAny<Issue>()))
            .Returns(Task.CompletedTask);
        _reputationServiceMock.Setup(r => r.AddPointsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        await _issueService.AddVoteAsync("issue1", "user1", VoteType.Up);

        // Assert
        Assert.Equal(6, issue.Upvotes);
    }

    [Fact]
    public async Task AddVoteAsync_WithDownvote_IncrementsDownvotes()
    {
        // Arrange
        var issue = new Issue { Id = "issue1", Title = "Test", Downvotes = 2, Reporter = new UserSummary { Id = "reporter1" } };
        
        _issueRepoMock.Setup(r => r.GetByIdAsync("issue1"))
            .ReturnsAsync(issue);
        _voteRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Vote, bool>>>()))
            .ReturnsAsync(new List<Vote>());
        _voteRepoMock.Setup(r => r.InsertAsync(It.IsAny<Vote>()))
            .ReturnsAsync((Vote v) => v);
        _issueRepoMock.Setup(r => r.ReplaceAsync("issue1", It.IsAny<Issue>()))
            .Returns(Task.CompletedTask);

        // Act
        await _issueService.AddVoteAsync("issue1", "user1", VoteType.Down);

        // Assert
        Assert.Equal(3, issue.Downvotes);
    }

    [Fact]
    public async Task RemoveVoteAsync_WithExistingVote_RemovesVoteSuccessfully()
    {
        // Arrange
        var issue = new Issue { Id = "issue1", Title = "Test", Upvotes = 5 };
        var existingVote = new Vote { Id = "vote1", UserId = "user1", IssueId = "issue1", Value = VoteType.Up };
        
        _issueRepoMock.Setup(r => r.GetByIdAsync("issue1"))
            .ReturnsAsync(issue);
        _voteRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Vote, bool>>>()))
            .ReturnsAsync(new List<Vote> { existingVote });
        _voteRepoMock.Setup(r => r.DeleteAsync("vote1"))
            .Returns(Task.CompletedTask);
        _issueRepoMock.Setup(r => r.ReplaceAsync("issue1", It.IsAny<Issue>()))
            .Returns(Task.CompletedTask);

        // Act
        await _issueService.RemoveVoteAsync("issue1", "user1");

        // Assert
        Assert.Equal(4, issue.Upvotes);
    }

    [Fact]
    public async Task GetIssueByIdAsync_WithExistingIssue_ReturnsIssue()
    {
        // Arrange
        var issue = new Issue { Id = "issue1", Title = "Test Issue" };
        
        _issueRepoMock.Setup(r => r.GetByIdAsync("issue1"))
            .ReturnsAsync(issue);

        // Act
        var result = await _issueService.GetIssueByIdAsync("issue1");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("issue1", result.Id);
        Assert.Equal("Test Issue", result.Title);
    }

    [Fact]
    public async Task GetIssueByIdAsync_WithNonexistentIssue_ReturnsNull()
    {
        // Arrange
        _issueRepoMock.Setup(r => r.GetByIdAsync("nonexistent"))
            .ReturnsAsync((Issue?)null);

        // Act
        var result = await _issueService.GetIssueByIdAsync("nonexistent");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task AddCommentAsync_WithValidComment_CreatesCommentSuccessfully()
    {
        // Arrange
        var issue = new Issue { Id = "issue1", Title = "Test", CommentCount = 0 };
        Comment? capturedComment = null;

        _issueRepoMock.Setup(r => r.GetByIdAsync("issue1"))
            .ReturnsAsync(issue);
        _commentRepoMock.Setup(r => r.InsertAsync(It.IsAny<Comment>()))
            .Callback<Comment>(c => capturedComment = c)
            .ReturnsAsync((Comment c) => c);
        _issueRepoMock.Setup(r => r.ReplaceAsync("issue1", It.IsAny<Issue>()))
            .Returns(Task.CompletedTask);
        _reputationServiceMock.Setup(r => r.AddPointsAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _issueService.AddCommentAsync("issue1", "user1", "Great issue!");

        // Assert
        Assert.NotNull(result);
        Assert.Equal("Great issue!", result.Text);
        Assert.Equal(1, issue.CommentCount);
    }

    [Fact]
    public async Task GetCommentsForIssueAsync_ReturnsIssueComments()
    {
        // Arrange
        var comments = new List<Comment>
        {
            new Comment { Id = "c1", IssueId = "issue1", Text = "Comment 1" },
            new Comment { Id = "c2", IssueId = "issue1", Text = "Comment 2" }
        };

        _commentRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Comment, bool>>>()))
            .ReturnsAsync(comments);

        // Act
        var result = await _issueService.GetCommentsForIssueAsync("issue1");

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task SoftDeleteIssueAsync_MarkIssueAsDeleted()
    {
        // Arrange
        var issue = new Issue { Id = "issue1", Title = "Test", IsDeleted = false };
        
        _issueRepoMock.Setup(r => r.GetByIdAsync("issue1"))
            .ReturnsAsync(issue);
        _issueRepoMock.Setup(r => r.ReplaceAsync("issue1", It.IsAny<Issue>()))
            .Returns(Task.CompletedTask);

        // Act
        await _issueService.SoftDeleteIssueAsync("issue1");

        // Assert
        Assert.True(issue.IsDeleted);
    }

    [Fact]
    public async Task RestoreIssueAsync_ClearsDeletedFlag()
    {
        // Arrange
        var issue = new Issue { Id = "issue1", Title = "Test", IsDeleted = true };
        
        _issueRepoMock.Setup(r => r.GetByIdAsync("issue1"))
            .ReturnsAsync(issue);
        _issueRepoMock.Setup(r => r.ReplaceAsync("issue1", It.IsAny<Issue>()))
            .Returns(Task.CompletedTask);

        // Act
        await _issueService.RestoreIssueAsync("issue1");

        // Assert
        Assert.False(issue.IsDeleted);
    }

    [Fact]
    public async Task GetIssuesByCityAsync_ReturnsIssuesInCity()
    {
        // Arrange
        var issues = new List<Issue>
        {
            new Issue { Id = "i1", CityId = "city1", Title = "Issue 1" },
            new Issue { Id = "i2", CityId = "city1", Title = "Issue 2" },
            new Issue { Id = "i3", CityId = "city2", Title = "Issue 3" }
        };

        _issueRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Issue, bool>>>()))
            .ReturnsAsync((Expression<Func<Issue, bool>> expr) =>
                issues.Where(expr.Compile()).ToList());

        // Act
        var result = await _issueService.GetIssuesByCityAsync("city1");

        // Assert
        Assert.Equal(2, result.Count);
        Assert.All(result, i => Assert.Equal("city1", i.CityId));
    }

    [Fact]
    public async Task GetAllIssuesAsync_ReturnsAllIssues()
    {
        // Arrange
        var issues = new List<Issue>
        {
            new Issue { Id = "i1", CityId = "city1", Title = "Issue 1" },
            new Issue { Id = "i2", CityId = "city2", Title = "Issue 2" }
        };

        _issueRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Issue, bool>>>()))
            .ReturnsAsync(issues);

        // Act
        var result = await _issueService.GetAllIssuesAsync();

        // Assert
        Assert.Equal(2, result.Total);
    }
}

