using Xunit;
using Moq;
using FixIt.Services;
using FixIt.Services.Contracts;
using FixIt.Services.Gamification;
using FixIt.Services.Background;
using FixIt.Data.Repository.Contracts;
using FixIt.Models.Issues;
using FixIt.Models.Common;
using FixIt.Models.Enums;
using FixIt.Models.AI;
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
    private readonly Mock<IIssueAnalysisQueue> _analysisQueueMock;
    private readonly Mock<UserManager<ApplicationUser>> _userManagerMock;
    private readonly Mock<ILogger<IssueService>> _loggerMock;
    private readonly IssueService _issueService;

    public IssueServiceTests()
    {
        _issueRepoMock = new Mock<IRepository<Issue>>();
        _tagRepoMock = new Mock<IRepository<Tag>>();
        _voteRepoMock = new Mock<IRepository<Vote>>();
        _viewEventRepoMock = new Mock<IRepository<ViewEvent>>();
        _commentRepoMock = new Mock<IRepository<Comment>>();
        _reputationServiceMock = new Mock<IReputationService>();
        _analysisQueueMock = new Mock<IIssueAnalysisQueue>();
        _loggerMock = new Mock<ILogger<IssueService>>();
        
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
            _analysisQueueMock.Object,
            _userManagerMock.Object,
            _loggerMock.Object
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
        await _issueService.UpdateIssueStatusAsync("issue1", IssueStatus.Confirmed, "user1");

        // Assert
        Assert.Equal(IssueStatus.Confirmed, issue.Status);
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

    // AddCommentAsync_* and GetCommentsForIssueAsync_* tests moved to
    // CommentServiceTests when comment methods were extracted from IssueService.

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

        _issueRepoMock.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<Issue, bool>>>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((Expression<Func<Issue, bool>> expr, int skip, int limit) =>
                new PagedResult<Issue>
                {
                    Items = issues.Where(expr.Compile()).Skip(skip).Take(limit),
                    Total = issues.LongCount()
                });

        // Act
        var result = await _issueService.GetAllIssuesAsync();

        // Assert
        Assert.Equal(2, result.Total);
    }

    [Fact]
    public async Task GetAllIssuesAsync_WithMostViewedSort_ReturnsIssuesOrderedByViews()
    {
        // Arrange
        var issues = new List<Issue>
        {
            new() { Id = "i1", Title = "Issue 1", ViewCount = 5, LastActivityAt = DateTime.UtcNow.AddHours(-3) },
            new() { Id = "i2", Title = "Issue 2", ViewCount = 25, LastActivityAt = DateTime.UtcNow.AddHours(-2) },
            new() { Id = "i3", Title = "Issue 3", ViewCount = 10, LastActivityAt = DateTime.UtcNow.AddHours(-1) }
        };

        _issueRepoMock.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<Issue, bool>>>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((Expression<Func<Issue, bool>> expr, int skip, int limit) =>
                new PagedResult<Issue>
                {
                    Items = issues.Where(expr.Compile()).Skip(skip).Take(limit),
                    Total = issues.LongCount()
                });

        // Act
        var result = await _issueService.GetAllIssuesAsync(sort: IssueSortOption.MostViewed);

        // Assert
        Assert.Equal(new[] { "i2", "i3", "i1" }, result.Items.Select(issue => issue.Id));
    }

    [Fact]
    public async Task GetAllIssuesAsync_WithCategoryAndDateRange_FiltersCorrectly()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var issues = new List<Issue>
        {
            new() { Id = "i1", Title = "Road", Category = IssueCategory.Infrastructure, CreatedAt = now.AddDays(-2), LastActivityAt = now.AddDays(-2) },
            new() { Id = "i2", Title = "Safety", Category = IssueCategory.PublicSafety, CreatedAt = now.AddDays(-1), LastActivityAt = now.AddDays(-1) },
            new() { Id = "i3", Title = "Old Road", Category = IssueCategory.Infrastructure, CreatedAt = now.AddDays(-40), LastActivityAt = now.AddDays(-40) }
        };

        _issueRepoMock.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<Issue, bool>>>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((Expression<Func<Issue, bool>> expr, int skip, int limit) =>
                new PagedResult<Issue>
                {
                    Items = issues.Where(expr.Compile()).Skip(skip).Take(limit),
                    Total = issues.Where(expr.Compile()).LongCount()
                });

        // Act
        var result = await _issueService.GetAllIssuesAsync(
            category: IssueCategory.Infrastructure,
            fromUtc: now.AddDays(-10),
            toUtc: now);

        // Assert
        Assert.Single(result.Items);
        Assert.Equal("i1", result.Items.First().Id);
    }

    [Fact]
    public async Task CreateIssueAsync_WithPriorityCategoryDepartment_PersistsOptionalFields()
    {
        // Arrange
        var reporter = new UserSummary { Id = "user1", DisplayName = "Test User" };
        Issue? capturedIssue = null;

        _issueRepoMock.Setup(r => r.InsertAsync(It.IsAny<Issue>()))
            .Callback<Issue>(issue => capturedIssue = issue)
            .ReturnsAsync((Issue issue) => issue);

        _tagRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Tag, bool>>>()))
            .ReturnsAsync(new List<Tag>());

        // Act
        await _issueService.CreateIssueAsync(
            "Water leakage",
            "Major pipe break in the street",
            0,
            0,
            "city1",
            reporter,
            priority: IssuePriority.High,
            category: IssueCategory.Utilities,
            department: "Utilities Department");

        // Assert
        Assert.NotNull(capturedIssue);
        Assert.Equal(IssuePriority.High, capturedIssue!.Priority);
        Assert.Equal(IssueCategory.Utilities, capturedIssue.Category);
        Assert.Equal("Utilities Department", capturedIssue.Department);
    }

    [Fact]
    public async Task CreateIssueAsync_WithAddress_SetsIssueAddress()
    {
        var reporter = new UserSummary { Id = "user1", DisplayName = "Test User" };
        Issue? capturedIssue = null;

        _issueRepoMock.Setup(r => r.InsertAsync(It.IsAny<Issue>()))
            .Callback<Issue>(issue => capturedIssue = issue)
            .ReturnsAsync((Issue issue) => issue);

        _tagRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Tag, bool>>>()))
            .ReturnsAsync(new List<Tag>());

        await _issueService.CreateIssueAsync(
            "Broken sign",
            "Stop sign missing reflective coating",
            23.3219,
            42.6977,
            "city1",
            reporter,
            address: "  ул. Витоша 1, София  ");

        Assert.NotNull(capturedIssue);
        Assert.Equal("ул. Витоша 1, София", capturedIssue!.Address);
    }

    [Fact]
    public async Task CreateIssueAsync_WithNullAddress_LeavesIssueAddressNull()
    {
        var reporter = new UserSummary { Id = "user1", DisplayName = "Test User" };
        Issue? capturedIssue = null;

        _issueRepoMock.Setup(r => r.InsertAsync(It.IsAny<Issue>()))
            .Callback<Issue>(issue => capturedIssue = issue)
            .ReturnsAsync((Issue issue) => issue);

        _tagRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Tag, bool>>>()))
            .ReturnsAsync(new List<Tag>());

        await _issueService.CreateIssueAsync(
            "Broken sign",
            "Stop sign missing reflective coating",
            0,
            0,
            "city1",
            reporter);

        Assert.NotNull(capturedIssue);
        Assert.Null(capturedIssue!.Address);
    }

    [Fact]
    public async Task GetPublicIssueOverviewAsync_ReturnsCountsAndFeaturedIssues()
    {
        // Arrange
        var now = DateTime.UtcNow;
        var issues = new List<Issue>
        {
            new() { Id = "i1", Title = "New", CityId = "city1", Status = IssueStatus.New, Priority = IssuePriority.High, LastActivityAt = now.AddHours(-4), CreatedAt = now.AddDays(-5) },
            new() { Id = "i2", Title = "Confirmed", CityId = "city2", Status = IssueStatus.Confirmed, Priority = IssuePriority.Medium, LastActivityAt = now.AddHours(-1), CreatedAt = now.AddDays(-4) },
            new() { Id = "i3", Title = "In Progress", CityId = "city2", Status = IssueStatus.InProgress, Priority = IssuePriority.Critical, LastActivityAt = now.AddHours(-2), CreatedAt = now.AddDays(-3) },
            new() { Id = "i4", Title = "Fixed", CityId = "city3", Status = IssueStatus.Fixed, Priority = IssuePriority.Low, LastActivityAt = now.AddHours(-3), CreatedAt = now.AddDays(-2) },
            new() { Id = "i5", Title = "Archived", CityId = "city3", Status = IssueStatus.Archived, Priority = IssuePriority.Low, LastActivityAt = now.AddHours(-5), CreatedAt = now.AddDays(-1) }
        };

        _issueRepoMock.Setup(r => r.FindAsync(It.IsAny<Expression<Func<Issue, bool>>>()))
            .ReturnsAsync((Expression<Func<Issue, bool>> expr) => issues.Where(expr.Compile()).ToList());
        _issueRepoMock.Setup(r => r.CountAsync(It.IsAny<Expression<Func<Issue, bool>>>()))
            .ReturnsAsync((Expression<Func<Issue, bool>> expr) =>
                issues.Where(expr.Compile()).LongCount());
        _issueRepoMock.Setup(r => r.QueryAsync(It.IsAny<Expression<Func<Issue, bool>>>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync((Expression<Func<Issue, bool>> expr, int skip, int limit) =>
                new PagedResult<Issue>
                {
                    Items = issues.Where(expr.Compile()).Skip(skip).Take(limit),
                    Total = issues.Where(expr.Compile()).LongCount()
                });

        // Act
        var result = await _issueService.GetPublicIssueOverviewAsync(2);

        // Assert
        Assert.Equal(5, result.TotalIssues);
        Assert.Equal(1, result.NewIssues);
        Assert.Equal(1, result.ConfirmedIssues);
        Assert.Equal(1, result.InProgressIssues);
        Assert.Equal(1, result.FixedIssues);
        Assert.Equal(1, result.CriticalIssues);
        Assert.Equal(3, result.CitiesCovered);
        Assert.Equal(3, result.ActiveIssues);
        // Featured issues are the first 2 from the repo, sorted by MostVoted (by LastActivityAt since all have 0 votes)
        // i1 (older) and i2 (more recent) from repo are sorted to [i2, i1] by most recent activity
        Assert.Equal(new[] { "i2", "i1" }, result.FeaturedIssues.Select(issue => issue.Id));
    }
}
