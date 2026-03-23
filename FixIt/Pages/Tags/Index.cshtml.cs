using FixIt.Services.Contracts;
using FixIt.Models.Issues;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FixIt.Pages.Tags;

public class TagIssuesModel : PageModel
{
    private readonly IIssueService _issueService;
    private readonly ITagService _tagService;
    private readonly ILogger<TagIssuesModel> _logger;

    public TagIssuesModel(
        IIssueService issueService,
        ITagService tagService,
        ILogger<TagIssuesModel> logger)
    {
        _issueService = issueService;
        _tagService = tagService;
        _logger = logger;
    }

    public string Tag { get; set; } = string.Empty;
    public bool TagFound { get; set; } = false;
    public List<Issue> Issues { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public int TotalPages { get; set; } = 1;

    public async Task OnGetAsync(string tag, int page = 1)
    {
        Tag = tag?.Trim() ?? string.Empty;
        CurrentPage = Math.Max(1, page);

        if (string.IsNullOrWhiteSpace(Tag))
        {
            TagFound = false;
            Issues = new List<Issue>();
            TotalPages = 1;
            CurrentPage = 1;
            return;
        }

        try
        {
            var normalizedTag = Tag.ToLowerInvariant();
            var tagEntity = await _tagService.GetTagByNameAsync(normalizedTag);

            if (tagEntity == null)
            {
                TagFound = false;
                Issues = new List<Issue>();
                TotalPages = 1;
                return;
            }

            TagFound = true;

            var result = await _issueService.GetIssuesByTagAsync(tagEntity.Id, CurrentPage, PageSize);
            Issues = result.Items.ToList();
            TotalPages = (int)Math.Ceiling(result.Total / (double)PageSize);

            if (CurrentPage > TotalPages && TotalPages > 0)
            {
                CurrentPage = TotalPages;
            }

            if (TotalPages == 0)
            {
                TotalPages = 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading issues for tag {Tag}", Tag);
            TagFound = false;
            Issues = new List<Issue>();
            TotalPages = 1;
            CurrentPage = 1;
        }
    }
}