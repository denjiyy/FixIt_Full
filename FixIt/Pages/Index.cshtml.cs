using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FixIt.Models.Issues;
using FixIt.Services.Contracts;

namespace FixIt.Pages;

public class IndexModel : PageModel
{
    private readonly ILogger<IndexModel> _logger;
    private readonly IIssueService _issueService;

    public IndexModel(ILogger<IndexModel> logger, IIssueService issueService)
    {
        _logger = logger;
        _issueService = issueService;
    }

    public IssuePublicOverview Overview { get; private set; } = new();

    public async Task OnGetAsync()
    {
        try
        {
            Overview = await _issueService.GetPublicIssueOverviewAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load homepage issue overview.");
            Overview = new IssuePublicOverview();
        }
    }
}
