using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FixIt.Pages;

public class NotFoundModel : PageModel
{
    private readonly ILogger<NotFoundModel> _logger;

    public NotFoundModel(ILogger<NotFoundModel> logger)
    {
        _logger = logger;
    }

    public void OnGet()
    {
        _logger.LogInformation("404 Not Found: {Path}", HttpContext.Request.Path);
    }
}
