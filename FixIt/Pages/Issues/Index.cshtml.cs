using Microsoft.AspNetCore.Mvc.RazorPages;
using FixIt.Services.Contracts;
using FixIt.Models.Enums;

namespace FixIt.Pages.Issues;

public class IssueListModel : PageModel
{
    private readonly IIssueService _issueService;

    public IssueListModel(IIssueService issueService)
    {
        _issueService = issueService;
    }

    public List<dynamic> Issues { get; set; } = new();
    public int CurrentPage { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public int TotalPages { get; set; } = 1;
    public string SearchQuery { get; set; } = "";
    public int SelectedStatus { get; set; } = -1;
    public int SelectedPriority { get; set; } = -1;

    public async Task OnGetAsync(int page = 1, string searchQuery = "", int status = -1, int priority = -1)
    {
        CurrentPage = Math.Max(1, page);
        SearchQuery = searchQuery ?? "";
        SelectedStatus = status;
        SelectedPriority = priority;

        try
        {
            // TODO: Call service to get issues
            // For now, return empty list
            Issues = new List<dynamic>();
            TotalPages = 1;
        }
        catch (Exception ex)
        {
            // Log error
            ModelState.AddModelError("", "Failed to load issues. Please try again.");
        }
    }
}
