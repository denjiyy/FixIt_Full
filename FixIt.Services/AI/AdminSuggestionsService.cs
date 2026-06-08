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
    private readonly ILogger<AdminSuggestionsService> _logger;

    public AdminSuggestionsService(
        IRepository<AdminSuggestion> suggestionRepository,
        IRepository<ContentReport> reportRepository,
        IRepository<Issue> issueRepository,
        IRepository<ApplicationUser> userRepository,
        ILogger<AdminSuggestionsService> logger)
    {
        _suggestionRepository = suggestionRepository;
        _reportRepository = reportRepository;
        _issueRepository = issueRepository;
        _userRepository = userRepository;
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

        // Check if we already have a recent suggestion (calculate cutoff in C# to avoid MongoDB serialization issues)
        var cutoffTime = DateTime.UtcNow.AddHours(-24);
        var existing = await _suggestionRepository.FindAsync(s => 
            s.TargetEntityId == reportId && 
            s.TargetEntityType == "Report" &&
            s.IsValid &&
            !s.IsActedUpon &&
            s.GeneratedAt > cutoffTime);

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
        return await SuggestIssueActionsInternalAsync(issueId, autoSave: true);
    }

    /// <summary>
    /// Internal method to generate suggestions for an issue with optional auto-save
    /// </summary>
    private async Task<List<AdminSuggestion>> SuggestIssueActionsInternalAsync(string issueId, bool autoSave)
    {
        var suggestions = new List<AdminSuggestion>();
        var issue = await _issueRepository.GetByIdAsync(issueId);

        if (issue == null)
        {
            _logger.LogWarning("Issue {IssueId} not found for suggestion generation", issueId);
            return suggestions;
        }

        // Check for existing recent suggestions to avoid duplicates (calculate cutoff in C# to avoid MongoDB serialization issues)
        var cutoffTime24h = DateTime.UtcNow.AddHours(-24);
        var existingRecent = await _suggestionRepository.FindAsync(s =>
            s.TargetEntityId == issueId &&
            s.TargetEntityType == "Issue" &&
            s.IsValid &&
            !s.IsActedUpon &&
            s.GeneratedAt > cutoffTime24h);

        var existingTypes = new HashSet<SuggestionType>(existingRecent.Select(s => s.Type));

        // Suggestion 1: Priority upgrade based on rules (highest priority)
        if (!existingTypes.Contains(SuggestionType.IssuePriority))
        {
            var prioritySuggestion = AnalyzeIssuePriority(issue);
            if (prioritySuggestion != null)
            {
                suggestions.Add(prioritySuggestion);
                existingTypes.Add(SuggestionType.IssuePriority);
            }
        }

        // Suggestion 2: Resolution recommendation for old open issues (second priority)
        if (!existingTypes.Contains(SuggestionType.IssueResolution))
        {
            var staleSuggestion = AnalyzeStaleIssue(issue);
            if (staleSuggestion != null)
            {
                suggestions.Add(staleSuggestion);
                existingTypes.Add(SuggestionType.IssueResolution);
            }
        }

        // Suggestion 3: Check for duplicate issues only if no priority issues detected
        if (!existingTypes.Contains(SuggestionType.IssueDuplicateWarning) && !suggestions.Any(s => s.Type == SuggestionType.IssuePriority))
        {
            var duplicateSuggestion = await AnalyzeForDuplicatesAsync(issue);
            if (duplicateSuggestion != null)
                suggestions.Add(duplicateSuggestion);
        }

        // Suggestion 4: Engagement-based suggestions only if no other issues detected
        if (!existingTypes.Contains(SuggestionType.IssuePriority) && !suggestions.Any())
        {
            var engagementSuggestion = AnalyzeEngagement(issue);
            if (engagementSuggestion != null)
                suggestions.Add(engagementSuggestion);
        }

        // Save only the most relevant suggestions (max 2 per issue)
        if (autoSave)
        {
            foreach (var suggestion in suggestions.Take(2))
            {
                try
                {
                    await _suggestionRepository.InsertAsync(suggestion);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save issue suggestion for issue {IssueId}", issueId);
                }
            }
        }

        _logger.LogInformation("Generated {Count} rule-based suggestions for issue {IssueId}", suggestions.Count, issueId);
        return suggestions;
    }

    /// <summary>
    /// Analyze issue priority based on rules (no AI required)
    /// </summary>
    private AdminSuggestion? AnalyzeIssuePriority(Issue issue)
    {
        // Keywords that indicate high-severity issues
        var criticalKeywords = new[] { "dangerous", "safety", "hazard", "accident", "injury", "broken", "failure", "critical" };
        var highKeywords = new[] { "blocked", "blocked road", "obstruction", "urgent", "emergency", "severe" };

        var titleLower = (issue.Title ?? "").ToLowerInvariant();
        var descLower = (issue.Description ?? "").ToLowerInvariant();
        var combined = $"{titleLower} {descLower}";

        // Check for critical indicators
        if (criticalKeywords.Any(k => combined.Contains(k)) && issue.Priority != IssuePriority.Critical)
        {
            return new AdminSuggestion
            {
                Type = SuggestionType.IssuePriority,
                TargetEntityId = issue.Id,
                TargetEntityType = "Issue",
                Title = "Priority Upgrade Recommended",
                Description = "Issue content suggests high severity and may warrant critical priority",
                ConfidenceScore = 75,
                ConfidenceLevel = ConfidenceLevel.High,
                RecommendedAction = "Upgrade to Critical",
                Reasoning = "Issue contains critical severity keywords indicating potential safety concern",
                SupportingData = new() { $"Current Priority: {issue.Priority}", "Content Pattern: High-severity keywords detected" }
            };
        }

        // Check for high-priority indicators
        if (highKeywords.Any(k => combined.Contains(k)) && issue.Priority == IssuePriority.Low)
        {
            return new AdminSuggestion
            {
                Type = SuggestionType.IssuePriority,
                TargetEntityId = issue.Id,
                TargetEntityType = "Issue",
                Title = "Priority Adjustment Suggested",
                Description = "Issue shows characteristics of medium/high priority work",
                ConfidenceScore = 65,
                ConfidenceLevel = ConfidenceLevel.Medium,
                RecommendedAction = "Upgrade to High",
                Reasoning = "Issue content indicates elevated priority compared to current classification",
                SupportingData = new() { $"Current Priority: {issue.Priority}", "Content Pattern: Medium/high severity keywords detected" }
            };
        }

        return null;
    }

    /// <summary>
    /// Analyze for potential duplicate issues using string similarity
    /// </summary>
    private async Task<AdminSuggestion?> AnalyzeForDuplicatesAsync(Issue issue)
    {
        try
        {
            var allIssues = await _issueRepository.FindAsync(i => 
                i.Id != issue.Id && 
                i.Status != IssueStatus.Rejected && 
                i.Status != IssueStatus.Duplicate &&
                i.CreatedAt > DateTime.UtcNow.AddDays(-30)); // Look back 30 days

            var potentialDuplicates = new List<(string Id, string Title, int similarity)>();
            var keywords = ExtractKeywords(issue.Title);

            foreach (var otherIssue in allIssues)
            {
                var similarity = CalculateSimilarity(issue.Title, otherIssue.Title, keywords);
                if (similarity >= 60) // 60% similarity threshold
                {
                    potentialDuplicates.Add((otherIssue.Id, otherIssue.Title, similarity));
                }
            }

            if (potentialDuplicates.Any())
            {
                var topMatches = potentialDuplicates.OrderByDescending(d => d.similarity).Take(3).ToList();
                return new AdminSuggestion
                {
                    Type = SuggestionType.IssueDuplicateWarning,
                    TargetEntityId = issue.Id,
                    TargetEntityType = "Issue",
                    Title = $"Possible Duplicate Issues Found ({topMatches.Count})",
                    Description = $"Found {potentialDuplicates.Count} potentially duplicate issue(s) that may need consolidation",
                    ConfidenceScore = Math.Min(85, topMatches[0].similarity),
                    ConfidenceLevel = ConfidenceLevel.High,
                    RecommendedAction = "Review & Consolidate",
                    Reasoning = "Similar issue titles detected - may represent same problem",
                    RelatedEntityIds = topMatches.Select(d => d.Id).ToList(),
                    SupportingData = topMatches.Select(d => $"'{d.Title}' — {d.similarity}% match").ToList()
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing duplicates for issue {IssueId}", issue.Id);
        }

        return null;
    }

    /// <summary>
    /// Analyze if issue is stale and should be resolved or rejected
    /// </summary>
    private AdminSuggestion? AnalyzeStaleIssue(Issue issue)
    {
        var issueAge = DateTime.UtcNow - issue.CreatedAt;

        // Issue open 30+ days - needs review
        if (issueAge.TotalDays > 30 && issue.Status != IssueStatus.Fixed && issue.Status != IssueStatus.Rejected && issue.Status != IssueStatus.Archived)
        {
            var confidenceScore = Math.Min(80, 40 + (int)issueAge.TotalDays / 10); // Higher confidence for older issues
            return new AdminSuggestion
            {
                Type = SuggestionType.IssueResolution,
                TargetEntityId = issue.Id,
                TargetEntityType = "Issue",
                Title = $"Long-Open Issue ({(int)issueAge.TotalDays} days)",
                Description = $"This issue has been open for {(int)issueAge.TotalDays} days. Consider reviewing status or closing it.",
                ConfidenceScore = confidenceScore,
                ConfidenceLevel = issueAge.TotalDays > 60 ? ConfidenceLevel.High : ConfidenceLevel.Medium,
                RecommendedAction = "Review & Update Status",
                Reasoning = $"Issue created {issue.CreatedAt:g} remains unresolved. Long-open issues may indicate: resolved but not marked, low priority, or abandoned.",
                SupportingData = new()
                {
                    $"Issue Age: {(int)issueAge.TotalDays} days",
                    $"Current Status: {issue.Status}",
                    $"Upvotes: {issue.Upvotes} | Views: {issue.ViewCount}"
                }
            };
        }

        return null;
    }

    /// <summary>
    /// Analyze engagement metrics for suggestions
    /// </summary>
    private AdminSuggestion? AnalyzeEngagement(Issue issue)
    {
        // Issue with high engagement but not high priority
        if ((issue.Upvotes + issue.ViewCount) > 50 && issue.Priority == IssuePriority.Low)
        {
            return new AdminSuggestion
            {
                Type = SuggestionType.IssuePriority,
                TargetEntityId = issue.Id,
                TargetEntityType = "Issue",
                Title = "High-Interest Issue - Priority Mismatch",
                Description = "This issue has significant community engagement but low priority",
                ConfidenceScore = 70,
                ConfidenceLevel = ConfidenceLevel.High,
                RecommendedAction = "Consider Priority Upgrade",
                Reasoning = "Community engagement levels suggest this issue warrants higher attention",
                SupportingData = new()
                {
                    $"Total Upvotes: {issue.Upvotes}",
                    $"View Count: {issue.ViewCount}",
                    $"Current Priority: {issue.Priority}"
                }
            };
        }

        return null;
    }

    /// <summary>
    /// Extract keywords from text for similarity matching
    /// </summary>
    private List<string> ExtractKeywords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new();

        var stopWords = new[] { "the", "a", "an", "and", "or", "but", "in", "on", "at", "to", "for", "of", "is", "are", "be" };
        return text.ToLowerInvariant()
            .Split(new[] { ' ', ',', '.', '?', '!' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !stopWords.Contains(w))
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Calculate similarity between two titles (0-100)
    /// </summary>
    private int CalculateSimilarity(string title1, string title2, List<string> keywords)
    {
        if (string.IsNullOrWhiteSpace(title1) || string.IsNullOrWhiteSpace(title2))
            return 0;

        var title2Lower = title2.ToLowerInvariant();
        var matchedKeywords = keywords.Count(k => title2Lower.Contains(k));
        var keywordMatchPercentage = keywords.Any() ? (matchedKeywords * 100) / keywords.Count : 0;

        // Levenshtein distance for overall similarity
        var levenshtein = LevenshteinDistance(title1.ToLowerInvariant(), title2Lower);
        var maxLen = Math.Max(title1.Length, title2.Length);
        var levenshteinSimilarity = maxLen > 0 ? ((maxLen - levenshtein) * 100) / maxLen : 0;

        // Weighted average: 60% keyword match, 40% Levenshtein
        return (keywordMatchPercentage * 60 + levenshteinSimilarity * 40) / 100;
    }

    /// <summary>
    /// Calculate Levenshtein distance between two strings
    /// </summary>
    private int LevenshteinDistance(string s1, string s2)
    {
        var len1 = s1.Length;
        var len2 = s2.Length;
        var d = new int[len1 + 1, len2 + 1];

        for (int i = 0; i <= len1; i++) d[i, 0] = i;
        for (int j = 0; j <= len2; j++) d[0, j] = j;

        for (int i = 1; i <= len1; i++)
        {
            for (int j = 1; j <= len2; j++)
            {
                var cost = s1[i - 1] == s2[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
            }
        }

        return d[len1, len2];
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
        // Calculate cutoff date in C# to avoid MongoDB serialization issues with TimeSpan
        var cutoffDate = DateTime.UtcNow.AddDays(-7);
        var suggestions = (await _suggestionRepository.FindAsync(s => 
            s.IsValid && 
            !s.IsActedUpon &&
            s.GeneratedAt > cutoffDate)).ToList();

        // Ensure variety - limit each suggestion type to at most 1 in results
        var diverseSuggestions = new Dictionary<SuggestionType, AdminSuggestion>();
        foreach (var suggestion in suggestions.OrderByDescending(s => s.ConfidenceScore))
        {
            if (!diverseSuggestions.ContainsKey(suggestion.Type))
            {
                diverseSuggestions[suggestion.Type] = suggestion;
            }
        }
        suggestions = diverseSuggestions.Values.ToList();

        // If no diverse suggestions exist or insufficient variety, regenerate with deduplication
        if (suggestions.Count < 2 || diverseSuggestions.Count < 2)
        {
            _logger.LogInformation("Insufficient variety in suggestions ({Count} diverse types), regenerating", diverseSuggestions.Count);

            var pendingIssues = await _issueRepository.FindAsync(i =>
                i.Status != IssueStatus.Fixed &&
                i.Status != IssueStatus.Rejected &&
                i.Status != IssueStatus.Duplicate &&
                i.Status != IssueStatus.Archived);

            // Generate suggestions with deduplication to avoid repetitive types
            var suggestionsByType = new Dictionary<SuggestionType, AdminSuggestion>();
            
            foreach (var issue in pendingIssues.Take(10)) // Look at up to 10 issues
            {
                try
                {
                    // Generate without auto-saving for deduplication
                    var issueSuggestions = await SuggestIssueActionsInternalAsync(issue.Id, autoSave: false);
                    
                    // Keep highest confidence suggestion of each type
                    foreach (var suggestion in issueSuggestions)
                    {
                        if (!suggestionsByType.ContainsKey(suggestion.Type) ||
                            suggestionsByType[suggestion.Type].ConfidenceScore < suggestion.ConfidenceScore)
                        {
                            suggestionsByType[suggestion.Type] = suggestion;
                        }
                    }
                    
                    // Stop after getting diverse suggestions
                    if (suggestionsByType.Count >= 5)
                        break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error generating suggestions for issue {IssueId}", issue.Id);
                }
            }
            
            // Save only the deduplicated suggestions
            suggestions = suggestionsByType.Values.ToList();
            foreach (var suggestion in suggestions)
            {
                try
                {
                    await _suggestionRepository.InsertAsync(suggestion);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to save deduplicated suggestion of type {Type}", suggestion.Type);
                }
            }
            
            _logger.LogInformation("Generated and saved {Count} deduplicated suggestions of {TypeCount} types", suggestions.Count, suggestionsByType.Count);
        }

        return suggestions
            .Where(s => s.IsValid)
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
