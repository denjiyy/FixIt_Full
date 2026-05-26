using FixIt.Data.Repository.Contracts;
using FixIt.Models.AI;
using FixIt.Models.Issues;
using FixIt.Models.Moderation;
using FixIt.Models.Users;
using FixIt.Models.Enums;
using Microsoft.Extensions.Logging;

namespace FixIt.Services.AI;

/// <summary>
/// Service for generating AI-powered suggestions for admin/moderator actions
/// </summary>
public interface IAdminSuggestionsService
{
    /// <summary>
    /// Generate suggestion for a content report
    /// </summary>
    Task<AdminSuggestion?> SuggestReportActionAsync(string reportId);

    /// <summary>
    /// Generate suggestions for an issue
    /// </summary>
    Task<List<AdminSuggestion>> SuggestIssueActionsAsync(string issueId);

    /// <summary>
    /// Generate suggestion for user moderation
    /// </summary>
    Task<AdminSuggestion?> SuggestUserModerationAsync(string userId);

    /// <summary>
    /// Get pending suggestions for admin dashboard
    /// </summary>
    Task<List<AdminSuggestion>> GetPendingSuggestionsAsync(int limit = 10);

    /// <summary>
    /// Get suggestions for a specific entity
    /// </summary>
    Task<List<AdminSuggestion>> GetSuggestionsForEntityAsync(string entityId, string entityType);

    /// <summary>
    /// Mark suggestion as acted upon
    /// </summary>
    Task MarkAsActedAsync(string suggestionId, string actionTaken, string userId);

    /// <summary>
    /// Invalidate a suggestion
    /// </summary>
    Task InvalidateSuggestionAsync(string suggestionId, string userId);

    /// <summary>
    /// Get suggestion by ID
    /// </summary>
    Task<AdminSuggestion?> GetSuggestionAsync(string suggestionId);
}

public class AdminSuggestionsService : IAdminSuggestionsService
{
    private readonly IRepository<AdminSuggestion> _suggestionRepository;
    private readonly IRepository<ContentReport> _reportRepository;
    private readonly IRepository<Issue> _issueRepository;
    private readonly IRepository<ApplicationUser> _userRepository;
    private readonly IIssueAnalysisService _analysisService;
    private readonly ILogger<AdminSuggestionsService> _logger;

    public AdminSuggestionsService(
        IRepository<AdminSuggestion> suggestionRepository,
        IRepository<ContentReport> reportRepository,
        IRepository<Issue> issueRepository,
        IRepository<ApplicationUser> userRepository,
        IIssueAnalysisService analysisService,
        ILogger<AdminSuggestionsService> logger)
    {
        _suggestionRepository = suggestionRepository;
        _reportRepository = reportRepository;
        _issueRepository = issueRepository;
        _userRepository = userRepository;
        _analysisService = analysisService;
        _logger = logger;
    }

    /// <summary>
    /// Generate suggestion for a content report
    /// </summary>
    public async Task<AdminSuggestion?> SuggestReportActionAsync(string reportId)
    {
        var report = await _reportRepository.GetByIdAsync(reportId);
        if (report == null)
        {
            _logger.LogWarning("Report {ReportId} not found for suggestion generation", reportId);
            return null;
        }

        // Check if we already have a recent suggestion
        var existing = await _suggestionRepository.FindAsync(s => 
            s.TargetEntityId == reportId && 
            s.TargetEntityType == "Report" &&
            s.IsValid &&
            !s.IsActedUpon &&
            (DateTime.UtcNow - s.GeneratedAt).TotalHours < 24);

        if (existing.Any())
        {
            _logger.LogInformation("Recent suggestion already exists for report {ReportId}", reportId);
            return existing.First();
        }

        // Analyze the report content and determine recommendation
        var (confidence, action, reasoning) = AnalyzeReportContent(report);

        if (confidence < 30) // Too low confidence
            return null;

        var suggestion = new AdminSuggestion
        {
            Type = SuggestionType.ReportAction,
            TargetEntityId = reportId,
            TargetEntityType = "Report",
            Title = $"Report Moderation Suggestion",
            Description = $"Based on analysis of this {report.Reason} report, we recommend: {action}",
            ConfidenceScore = confidence,
            ConfidenceLevel = confidence switch
            {
                >= 85 => ConfidenceLevel.VeryHigh,
                >= 70 => ConfidenceLevel.High,
                >= 50 => ConfidenceLevel.Medium,
                _ => ConfidenceLevel.Low
            },
            RecommendedAction = action,
            Reasoning = reasoning,
            SupportingData = new()
            {
                $"Report Reason: {report.Reason}",
                $"Report Status: {report.Status}",
                $"Report Age: {(DateTime.UtcNow - report.CreatedAt).Days} days"
            }
        };

        try
        {
            await _suggestionRepository.InsertAsync(suggestion);
            _logger.LogInformation("Generated report action suggestion for report {ReportId} with confidence {Confidence}", reportId, confidence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save report suggestion");
            return null;
        }

        return suggestion;
    }

    /// <summary>
    /// Generate suggestions for an issue
    /// </summary>
    public async Task<List<AdminSuggestion>> SuggestIssueActionsAsync(string issueId)
    {
        var suggestions = new List<AdminSuggestion>();
        var issue = await _issueRepository.GetByIdAsync(issueId);

        if (issue == null)
        {
            _logger.LogWarning("Issue {IssueId} not found for suggestion generation", issueId);
            return suggestions;
        }

        // Get AI analysis
        var analysis = await _analysisService.GetAnalysisAsync(issueId);

        // Suggestion 1: Priority adjustment based on severity
        if (analysis?.EstimatedSeverity >= 8 && issue.Priority != IssuePriority.Critical)
        {
            suggestions.Add(new AdminSuggestion
            {
                Type = SuggestionType.IssuePriority,
                TargetEntityId = issueId,
                TargetEntityType = "Issue",
                Title = "Priority May Need Upgrade",
                Description = "AI analysis indicates this issue is severe and should have higher priority",
                ConfidenceScore = Math.Min(95, analysis.ConfidenceScore + 10),
                ConfidenceLevel = ConfidenceLevel.High,
                RecommendedAction = "Update Priority to Critical",
                Reasoning = $"Estimated severity: {analysis.EstimatedSeverity}/10. Category: {analysis.Category}",
                SupportingData = new() { $"Analysis Confidence: {analysis.ConfidenceScore}%", $"Severity Score: {analysis.EstimatedSeverity}/10" }
            });
        }

        // Suggestion 2: Check for duplicates
        if (analysis?.PotentialDuplicates.Any() == true)
        {
            var duplicateCount = analysis.PotentialDuplicates.Count;
            suggestions.Add(new AdminSuggestion
            {
                Type = SuggestionType.IssueDuplicateWarning,
                TargetEntityId = issueId,
                TargetEntityType = "Issue",
                Title = $"Possible Duplicate Issues Found",
                Description = $"Found {duplicateCount} similar issue(s) that may be duplicates",
                ConfidenceScore = analysis.PotentialDuplicates.Max(d => d.SimilarityScore),
                ConfidenceLevel = ConfidenceLevel.High,
                RecommendedAction = "Review Related Issues",
                Reasoning = "Similar issues detected that may represent the same problem",
                RelatedEntityIds = analysis.PotentialDuplicates.Select(d => d.IssueId).ToList(),
                SupportingData = analysis.PotentialDuplicates.Select(d => $"Issue '{d.IssueTitle}' - {d.SimilarityScore}% match").ToList()
            });
        }

        // Suggestion 3: Resolution recommendation for old open issues
        var issueAge = DateTime.UtcNow - issue.CreatedAt;
        if (issueAge.TotalDays > 30 && issue.Status != IssueStatus.Fixed && issue.Status != IssueStatus.Rejected)
        {
            suggestions.Add(new AdminSuggestion
            {
                Type = SuggestionType.IssueResolution,
                TargetEntityId = issueId,
                TargetEntityType = "Issue",
                Title = "Long-Open Issue - Review for Resolution",
                Description = $"This issue has been open for {(int)issueAge.TotalDays} days. Consider if it can be resolved or rejected.",
                ConfidenceScore = 60,
                ConfidenceLevel = ConfidenceLevel.Medium,
                RecommendedAction = "Review Status",
                Reasoning = $"Issue opened on {issue.CreatedAt:g} and has not been resolved",
                SupportingData = new() { $"Issue Age: {(int)issueAge.TotalDays} days", $"Current Status: {issue.Status}" }
            });
        }

        // Save all suggestions
        foreach (var suggestion in suggestions)
        {
            try
            {
                await _suggestionRepository.InsertAsync(suggestion);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save issue suggestion");
            }
        }

        _logger.LogInformation("Generated {Count} suggestions for issue {IssueId}", suggestions.Count, issueId);
        return suggestions;
    }

    /// <summary>
    /// Generate suggestion for user moderation
    /// </summary>
    public async Task<AdminSuggestion?> SuggestUserModerationAsync(string userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User {UserId} not found for moderation suggestion", userId);
            return null;
        }

        // Check if user is already banned or restricted
        if (user.IsBanned)
        {
            _logger.LogInformation("User {UserId} is already banned, no suggestion needed", userId);
            return null;
        }

        // Analyze user behavior patterns
        var suspiciousPattern = AnalyzeUserBehavior(user);
        if (suspiciousPattern.confidence < 40)
            return null;

        var suggestion = new AdminSuggestion
        {
            Type = SuggestionType.UserModeration,
            TargetEntityId = userId,
            TargetEntityType = "User",
            Title = suspiciousPattern.title,
            Description = suspiciousPattern.description,
            ConfidenceScore = suspiciousPattern.confidence,
            ConfidenceLevel = suspiciousPattern.confidence switch
            {
                >= 85 => ConfidenceLevel.VeryHigh,
                >= 70 => ConfidenceLevel.High,
                >= 50 => ConfidenceLevel.Medium,
                _ => ConfidenceLevel.Low
            },
            RecommendedAction = suspiciousPattern.recommendedAction,
            Reasoning = suspiciousPattern.reasoning,
            SupportingData = suspiciousPattern.supportingData
        };

        try
        {
            await _suggestionRepository.InsertAsync(suggestion);
            _logger.LogInformation("Generated user moderation suggestion for {UserId} with confidence {Confidence}", userId, suspiciousPattern.confidence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save user moderation suggestion");
            return null;
        }

        return suggestion;
    }

    /// <summary>
    /// Get pending suggestions for admin dashboard
    /// </summary>
    public async Task<List<AdminSuggestion>> GetPendingSuggestionsAsync(int limit = 10)
    {
        var suggestions = await _suggestionRepository.FindAsync(s => 
            s.IsValid && 
            !s.IsActedUpon &&
            (DateTime.UtcNow - s.GeneratedAt).TotalDays < 7);

        return suggestions
            .OrderByDescending(s => s.ConfidenceScore)
            .Take(limit)
            .ToList();
    }

    /// <summary>
    /// Get suggestions for a specific entity
    /// </summary>
    public async Task<List<AdminSuggestion>> GetSuggestionsForEntityAsync(string entityId, string entityType)
    {
        var suggestions = await _suggestionRepository.FindAsync(s =>
            s.TargetEntityId == entityId &&
            s.TargetEntityType == entityType &&
            s.IsValid);

        return suggestions
            .OrderByDescending(s => s.ConfidenceScore)
            .ToList();
    }

    /// <summary>
    /// Mark suggestion as acted upon
    /// </summary>
    public async Task MarkAsActedAsync(string suggestionId, string actionTaken, string userId)
    {
        var suggestion = await _suggestionRepository.GetByIdAsync(suggestionId);
        if (suggestion == null)
        {
            _logger.LogWarning("Suggestion {SuggestionId} not found", suggestionId);
            return;
        }

        suggestion.IsActedUpon = true;
        suggestion.ActionTaken = actionTaken;
        suggestion.ActedByUserId = userId;
        suggestion.ActedAt = DateTime.UtcNow;

        await _suggestionRepository.ReplaceAsync(suggestionId, suggestion);
        _logger.LogInformation("Marked suggestion {SuggestionId} as acted: {ActionTaken}", suggestionId, actionTaken);
    }

    /// <summary>
    /// Invalidate a suggestion
    /// </summary>
    public async Task InvalidateSuggestionAsync(string suggestionId, string userId)
    {
        var suggestion = await _suggestionRepository.GetByIdAsync(suggestionId);
        if (suggestion == null)
        {
            _logger.LogWarning("Suggestion {SuggestionId} not found", suggestionId);
            return;
        }

        suggestion.IsValid = false;
        suggestion.InvalidatedByUserId = userId;
        suggestion.InvalidatedAt = DateTime.UtcNow;

        await _suggestionRepository.ReplaceAsync(suggestionId, suggestion);
        _logger.LogInformation("Invalidated suggestion {SuggestionId}", suggestionId);
    }

    /// <summary>
    /// Get suggestion by ID
    /// </summary>
    public async Task<AdminSuggestion?> GetSuggestionAsync(string suggestionId)
    {
        return await _suggestionRepository.GetByIdAsync(suggestionId);
    }

    #region Helper Methods

    private (int confidence, string action, string reasoning) AnalyzeReportContent(ContentReport report)
    {
        // Pattern analysis for report recommendation
        var details = report.Details?.ToLowerInvariant() ?? string.Empty;
        var reason = report.Reason.ToString().ToLowerInvariant();

        // High confidence patterns
        if (reason.Contains("spam") || details.Contains("advertisement") || details.Contains("promotional"))
            return (90, "Uphold", "Clear spam/promotional content pattern detected");

        if (reason.Contains("violence") || reason.Contains("threat") || details.Contains("aggressive language"))
            return (85, "Uphold", "Safety violation pattern detected");

        if (reason.Contains("duplicate") || reason.Contains("spam"))
            return (75, "Dismiss", "Likely false report of non-existent issue");

        // Medium confidence
        if (reason.Contains("harassment") || details.Contains("inappropriate"))
            return (65, "Uphold", "Potential community guidelines violation");

        if (reason.Contains("misinformation") || details.Contains("false information"))
            return (60, "Review", "Factual accuracy concern - manual review recommended");

        // Low confidence - needs human review
        return (40, "Review", "Standard report requiring manual assessment");
    }

    private (int confidence, string title, string description, string recommendedAction, string reasoning, List<string> supportingData) AnalyzeUserBehavior(ApplicationUser user)
    {
        // Confidence floors out below 50 by design: with only account-age and
        // email-verification signals we never reach the suggestion threshold on
        // those alone. Callers already short-circuit before this method when the
        // user is banned, so no IsBanned branch is needed here.
        var supportingData = new List<string>();
        var baseConfidence = 0;

        var accountAge = DateTime.UtcNow - user.CreatedAt;
        if (accountAge.TotalDays < 1)
        {
            baseConfidence += 15;
            supportingData.Add("Very new account (< 1 day old)");
        }

        if (!user.EmailConfirmed)
        {
            baseConfidence += 10;
            supportingData.Add("Email not verified");
        }

        if (baseConfidence > 50)
        {
            return (baseConfidence, "Suspicious User Activity",
                "User account shows potential spam or abuse patterns",
                "Review and Consider Restriction",
                "Account patterns match common spam/abuse indicators",
                supportingData);
        }

        return (baseConfidence, "", "", "", "", supportingData);
    }

    #endregion
}
