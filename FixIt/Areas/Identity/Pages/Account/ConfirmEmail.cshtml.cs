using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using FixIt.Models.Users;

namespace FixIt.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class ConfirmEmailModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ConfirmEmailModel> _logger;

    public ConfirmEmailModel(UserManager<ApplicationUser> userManager, ILogger<ConfirmEmailModel> logger)
    {
        _userManager = userManager;
        _logger = logger;
    }

    public bool Confirmed { get; private set; }

    public string StatusMessage { get; private set; } = string.Empty;

    public async Task<IActionResult> OnGetAsync(string? userId, string? code)
    {
        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(code))
        {
            return RedirectToPage("./Login");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            // Don't reveal whether the id matched a real account.
            _logger.LogWarning("Email confirmation attempted for unknown user id {UserId}.", userId);
            return Fail();
        }

        if (await _userManager.IsEmailConfirmedAsync(user))
        {
            Confirmed = true;
            StatusMessage = "Your email is already confirmed. You can sign in.";
            return Page();
        }

        string decodedToken;
        try
        {
            decodedToken = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code));
        }
        catch (FormatException)
        {
            return Fail();
        }

        var result = await _userManager.ConfirmEmailAsync(user, decodedToken);
        if (result.Succeeded)
        {
            _logger.LogInformation("Email confirmed for user {UserId}.", user.Id);
            Confirmed = true;
            StatusMessage = "Thank you — your email is confirmed. You can now sign in.";
            return Page();
        }

        _logger.LogWarning(
            "Email confirmation failed for user {UserId}: {Errors}",
            user.Id, string.Join("; ", result.Errors.Select(e => e.Description)));
        return Fail();
    }

    private PageResult Fail()
    {
        Confirmed = false;
        StatusMessage = "We couldn't confirm your email. The link may be invalid or expired — request a new one below.";
        return Page();
    }
}
