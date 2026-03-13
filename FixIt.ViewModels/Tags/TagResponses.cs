namespace FixIt.ViewModels.Tags;

/// <summary>
/// Response model for tag summary
/// </summary>
public class TagResponse
{
    public string Id { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Category { get; set; }
    public string? Description { get; set; }
    public int UsageCount { get; set; }
    public bool IsApproved { get; set; }
    public IEnumerable<string> Aliases { get; set; } = Array.Empty<string>();
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// Response model for paginated tags
/// </summary>
public class TagPageResponse
{
    public IEnumerable<TagResponse> Items { get; set; } = Array.Empty<TagResponse>();
    public long Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
}
