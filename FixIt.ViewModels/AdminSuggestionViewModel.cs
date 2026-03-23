using FixIt.Models.AI;

namespace FixIt.ViewModels;

/// <summary>
/// View model for displaying admin suggestions in UI
/// </summary>
public class AdminSuggestionViewModel
{
    public string Id { get; set; } = string.Empty;

    public SuggestionType Type { get; set; }

    public string TargetEntityId { get; set; } = string.Empty;

    public string TargetEntityType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public int ConfidenceScore { get; set; }

    public ConfidenceLevel ConfidenceLevel { get; set; }

    public string RecommendedAction { get; set; } = string.Empty;

    public string Reasoning { get; set; } = string.Empty;

    public List<string> SupportingData { get; set; } = new();

    public List<string> RelatedEntityIds { get; set; } = new();

    public bool IsActedUpon { get; set; }

    public string? ActionTaken { get; set; }

    public DateTime GeneratedAt { get; set; }

    public int HoursOld => (int)(DateTime.UtcNow - GeneratedAt).TotalHours;

    /// <summary>
    /// Get CSS class for confidence badge
    /// </summary>
    public string GetConfidenceBadgeClass()
    {
        return ConfidenceLevel switch
        {
            ConfidenceLevel.VeryHigh => "badge-success",
            ConfidenceLevel.High => "badge-info",
            ConfidenceLevel.Medium => "badge-warning",
            ConfidenceLevel.Low => "badge-secondary",
            _ => "badge-secondary"
        };
    }

    /// <summary>
    /// Get icon for suggestion type
    /// </summary>
    public string GetTypeIcon()
    {
        return Type switch
        {
            SuggestionType.ReportAction => "bi-exclamation-triangle",
            SuggestionType.IssuePriority => "bi-arrow-up",
            SuggestionType.IssueDuplicateWarning => "bi-diagram-2",
            SuggestionType.IssueResolution => "bi-check-circle",
            SuggestionType.UserModeration => "bi-shield-exclamation",
            SuggestionType.ResourceAllocation => "bi-people",
            _ => "bi-lightbulb"
        };
    }

    public static AdminSuggestionViewModel FromModel(AdminSuggestion suggestion)
    {
        return new AdminSuggestionViewModel
        {
            Id = suggestion.Id,
            Type = suggestion.Type,
            TargetEntityId = suggestion.TargetEntityId,
            TargetEntityType = suggestion.TargetEntityType,
            Title = suggestion.Title,
            Description = suggestion.Description,
            ConfidenceScore = suggestion.ConfidenceScore,
            ConfidenceLevel = suggestion.ConfidenceLevel,
            RecommendedAction = suggestion.RecommendedAction,
            Reasoning = suggestion.Reasoning,
            SupportingData = suggestion.SupportingData,
            RelatedEntityIds = suggestion.RelatedEntityIds,
            IsActedUpon = suggestion.IsActedUpon,
            ActionTaken = suggestion.ActionTaken,
            GeneratedAt = suggestion.GeneratedAt
        };
    }
}

/// <summary>
/// Dashboard view model with suggestions
/// </summary>
public class AdminDashboardWithSuggestionsViewModel
{
    public int TotalUsers { get; set; }

    public int TotalIssues { get; set; }

    public int TotalReports { get; set; }

    public int ResolvedIssues { get; set; }

    public int PendingReports { get; set; }

    public int ActiveModerators { get; set; }

    public List<AdminSuggestionViewModel> PendingSuggestions { get; set; } = new();

    public int TotalPendingSuggestions { get; set; }

    public int HighConfidenceSuggestions { get; set; }
}
