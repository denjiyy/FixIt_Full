using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FixIt.Models.Users;
using Microsoft.AspNetCore.Identity;

namespace FixIt.Pages.Settings;

[Authorize]
public class ConnectedAccountsModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ConnectedAccountsModel> _logger;

    public ConnectedAccountsModel(
        UserManager<ApplicationUser> userManager,
        ILogger<ConnectedAccountsModel> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public new ApplicationUser? User { get; set; }
    public string? SuccessMessage { get; set; }
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(string? success = null, string? error = null)
    {
        User = await _userManager.GetUserAsync(HttpContext.User);
        
        if (success == "linked")
            SuccessMessage = "Provider linked successfully!";
        if (error == "linking_failed")
            ErrorMessage = "Failed to link provider. Please try again.";
    }

    public async Task<IActionResult> OnPostUnlinkAsync(string provider)
    {
        var user = await _userManager.GetUserAsync(HttpContext.User);
        if (user == null)
            return Unauthorized();

        // Ensure at least one authentication method remains
        var otherProviders = user.ExternalIdentities.Where(e => e.Provider != provider).ToList();
        if (!user.HasPasswordAuth && otherProviders.Count == 0)
        {
            ErrorMessage = "Cannot unlink your only authentication method.";
            return RedirectToPage();
        }

        // Remove the provider
        var identity = user.ExternalIdentities.FirstOrDefault(e => e.Provider == provider);
        if (identity != null)
        {
            user.ExternalIdentities.Remove(identity);
            await _userManager.UpdateAsync(user);
            SuccessMessage = $"{provider} has been unlinked.";
            _logger.LogInformation($"User {user.Email} unlinked {provider}");
        }

        return RedirectToPage();
    }
}
