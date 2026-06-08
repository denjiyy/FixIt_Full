using FixIt.Data.Repository.Contracts;
using FixIt.Models.AI;
using FixIt.Models.Enums;
using FixIt.Models.Issues;
using FixIt.Models.Moderation;
using FixIt.Models.Users;
using FixIt.Services.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace FixIt.Tests.Services.AI;

/// <summary>
/// Verifies the admin duplicate-warning suggestion. It is raised by
/// <see cref="AdminSuggestionsService"/>'s own local string-similarity pass over
/// recent issues returned by the issue repository. The warning fires when a
/// similar issue exists and stays quiet when none do.
/// </summary>
public class AdminSuggestionsServiceTests
{
    private static AdminSuggestionsService CreateService(
        Mock<IRepository<AdminSuggestion>> suggestionRepo,
        Mock<IRepository<Issue>> issueRepo)
        => new(
            suggestionRepo.Object,
            Mock.Of<IRepository<ContentReport>>(),
            issueRepo.Object,
            Mock.Of<IRepository<ApplicationUser>>(),
            NullLogger<AdminSuggestionsService>.Instance);

    private static (Mock<IRepository<AdminSuggestion>>, Mock<IRepository<Issue>>) Mocks(
        string issueId, IEnumerable<Issue>? otherIssues = null)
    {
        var issue = new Issue
        {
            Id = issueId,
            Title = "Pothole on Vitosha Blvd",
            Description = "Deep pothole damaging cars.",
            Status = IssueStatus.New,
            Priority = IssuePriority.Medium,
            CreatedAt = DateTime.UtcNow, // recent → resolution suggestion stays quiet
        };

        // By default expose one near-identical issue so the service's local
        // duplicate-detection pass (_issueRepository.FindAsync) has a match to flag.
        // Tests exercising the "no duplicates" path pass an empty collection instead.
        otherIssues ??= new List<Issue>
        {
            new()
            {
                Id = "DUP1",
                Title = "Pothole on Vitosha Boulevard",
                Description = "Deep pothole damaging cars on the same boulevard.",
                Status = IssueStatus.New,
                Priority = IssuePriority.Medium,
                CreatedAt = DateTime.UtcNow.AddDays(-1),
            }
        };

        var suggestionRepo = new Mock<IRepository<AdminSuggestion>>();
        suggestionRepo.Setup(r => r.InsertAsync(It.IsAny<AdminSuggestion>()))
            .ReturnsAsync((AdminSuggestion s) => s);

        var issueRepo = new Mock<IRepository<Issue>>();
        issueRepo.Setup(r => r.GetByIdAsync(issueId)).ReturnsAsync(issue);

        issueRepo.Setup(r => r.FindAsync(It.IsAny<System.Linq.Expressions.Expression<Func<Issue, bool>>>()))
            .ReturnsAsync(otherIssues.ToList());

        return (suggestionRepo, issueRepo);
    }

    [Fact]
    public async Task SuggestIssueActionsAsync_WithPotentialDuplicates_RaisesDuplicateWarning()
    {
        const string issueId = "ISSUE1";

        var (suggestionRepo, issueRepo) = Mocks(issueId);
        var service = CreateService(suggestionRepo, issueRepo);

        var suggestions = await service.SuggestIssueActionsAsync(issueId);

        var duplicate = suggestions
            .FirstOrDefault(s => s.Type == SuggestionType.IssueDuplicateWarning);

        Assert.NotNull(duplicate);

        Assert.InRange(duplicate!.ConfidenceScore, 0, 100);
        Assert.Contains("DUP1", duplicate.RelatedEntityIds);

        suggestionRepo.Verify(
            r => r.InsertAsync(It.Is<AdminSuggestion>(s => s.Type == SuggestionType.IssueDuplicateWarning)),
            Times.Once);
    }

    [Fact]
    public async Task SuggestIssueActionsAsync_NoDuplicates_DoesNotRaiseDuplicateWarning()
    {
        string issueId = Guid.NewGuid().ToString();

        // No other issues exist, so the local duplicate-detection pass finds nothing to flag.
        var (suggestionRepo, issueRepo) = Mocks(issueId, otherIssues: new List<Issue>());
        var service = CreateService(suggestionRepo, issueRepo);

        var suggestions = await service.SuggestIssueActionsAsync(issueId);

        Assert.DoesNotContain(suggestions, s => s.Type == SuggestionType.IssueDuplicateWarning);
    }
}
