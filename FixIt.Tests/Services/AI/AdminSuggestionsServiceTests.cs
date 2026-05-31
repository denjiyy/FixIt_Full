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
/// Verifies the admin duplicate-warning suggestion — which keys off
/// <see cref="IssueAnalysis.PotentialDuplicates"/> — now fires, since that list
/// is populated by the local duplicate-detection pass.
/// </summary>
public class AdminSuggestionsServiceTests
{
    private static AdminSuggestionsService CreateService(
        Mock<IRepository<AdminSuggestion>> suggestionRepo,
        Mock<IRepository<Issue>> issueRepo,
        Mock<IIssueAnalysisService> analysisService)
        => new(
            suggestionRepo.Object,
            Mock.Of<IRepository<ContentReport>>(),
            issueRepo.Object,
            Mock.Of<IRepository<ApplicationUser>>(),
            analysisService.Object,
            NullLogger<AdminSuggestionsService>.Instance);

    private static (Mock<IRepository<AdminSuggestion>>, Mock<IRepository<Issue>>, Mock<IIssueAnalysisService>) Mocks(
        string issueId, IssueAnalysis analysis)
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

        var suggestionRepo = new Mock<IRepository<AdminSuggestion>>();
        suggestionRepo.Setup(r => r.InsertAsync(It.IsAny<AdminSuggestion>()))
            .ReturnsAsync((AdminSuggestion s) => s);

        var issueRepo = new Mock<IRepository<Issue>>();
        issueRepo.Setup(r => r.GetByIdAsync(issueId)).ReturnsAsync(issue);

        var analysisService = new Mock<IIssueAnalysisService>();
        analysisService.Setup(s => s.GetAnalysisAsync(issueId)).ReturnsAsync(analysis);

        return (suggestionRepo, issueRepo, analysisService);
    }

    [Fact]
    public async Task SuggestIssueActionsAsync_WithPotentialDuplicates_RaisesDuplicateWarning()
    {
        const string issueId = "ISSUE1";
        var analysis = new IssueAnalysis
        {
            IssueId = issueId,
            EstimatedSeverity = 5, // < 8 → priority-upgrade suggestion stays quiet
            ConfidenceScore = 70,
            PotentialDuplicates = new List<DuplicateMatch>
            {
                new() { IssueId = "DUP1", IssueTitle = "Similar pothole", SimilarityScore = 80, Reason = "Shared terms: pothole" },
            },
        };

        var (suggestionRepo, issueRepo, analysisService) = Mocks(issueId, analysis);
        var service = CreateService(suggestionRepo, issueRepo, analysisService);

        var suggestions = await service.SuggestIssueActionsAsync(issueId);

        var duplicate = suggestions.Single(s => s.Type == SuggestionType.IssueDuplicateWarning);
        Assert.Equal(80, duplicate.ConfidenceScore);
        Assert.Contains("DUP1", duplicate.RelatedEntityIds);
        suggestionRepo.Verify(
            r => r.InsertAsync(It.Is<AdminSuggestion>(s => s.Type == SuggestionType.IssueDuplicateWarning)),
            Times.Once);
    }

    [Fact]
    public async Task SuggestIssueActionsAsync_NoDuplicates_DoesNotRaiseDuplicateWarning()
    {
        const string issueId = "ISSUE2";
        var analysis = new IssueAnalysis
        {
            IssueId = issueId,
            EstimatedSeverity = 5,
            ConfidenceScore = 70,
            PotentialDuplicates = new List<DuplicateMatch>(),
        };

        var (suggestionRepo, issueRepo, analysisService) = Mocks(issueId, analysis);
        var service = CreateService(suggestionRepo, issueRepo, analysisService);

        var suggestions = await service.SuggestIssueActionsAsync(issueId);

        Assert.DoesNotContain(suggestions, s => s.Type == SuggestionType.IssueDuplicateWarning);
    }
}
