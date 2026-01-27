using System.ComponentModel.DataAnnotations;

namespace FixIt.ViewModels.Tags;

/// <summary>
/// Request model for creating a new tag
/// </summary>
public class CreateTagRequest
{
    [Required(ErrorMessage = "Tag name is required")]
    [StringLength(50, MinimumLength = 2, ErrorMessage = "Tag name must be between 2 and 50 characters")]
    public string Name { get; set; } = null!;

    [StringLength(100, ErrorMessage = "Category must not exceed 100 characters")]
    public string? Category { get; set; }

    [StringLength(500, ErrorMessage = "Description must not exceed 500 characters")]
    public string? Description { get; set; }
}

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
/// Request model for tag autocomplete
/// </summary>
public class TagAutocompleteRequest
{
    [Required(ErrorMessage = "Prefix is required")]
    [StringLength(50, MinimumLength = 1, ErrorMessage = "Prefix must be between 1 and 50 characters")]
    public string Prefix { get; set; } = null!;

    public int Limit { get; set; } = 10;
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
