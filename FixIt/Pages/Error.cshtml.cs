using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FixIt.Pages;

[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
[IgnoreAntiforgeryToken]
public class ErrorModel : PageModel
{
    public string? RequestId { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorMessage { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    private readonly ILogger<ErrorModel> _logger;

    public ErrorModel(ILogger<ErrorModel> logger)
    {
        _logger = logger;
    }

    public void OnGet()
    {
        RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        ErrorCode = HttpContext.Request.Query["code"].ToString();
        
        // Set user-friendly error message
        ErrorMessage = ErrorCode switch
        {
            "403" => "You don't have permission to access this resource.",
            "404" => "The page you're looking for could not be found.",
            "400" => "The request could not be understood by the server.",
            "500" => "An internal server error occurred.",
            "502" => "Bad gateway. The server is temporarily unavailable.",
            "503" => "Service unavailable. The server is temporarily unavailable.",
            _ => "An unexpected error occurred. Please try again later."
        };
        
        _logger.LogWarning("Error page displayed: Code={ErrorCode}, RequestId={RequestId}", ErrorCode, RequestId);
    }
}

