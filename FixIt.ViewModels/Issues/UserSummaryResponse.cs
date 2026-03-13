namespace FixIt.ViewModels.Issues;

/// <summary>
/// Response model for user summary
/// </summary>
public class UserSummaryResponse
{
    public string Id { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public string? AvatarUrl { get; set; }
}
