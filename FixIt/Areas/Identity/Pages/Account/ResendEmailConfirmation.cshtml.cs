using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using FixIt.Models.Users;
using FixIt.Services.Constants;
using FixIt.Services.Email;

namespace FixIt.Areas.Identity.Pages.Account;

[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicyNames.AuthStrict)]
public class ResendEmailConfirmationModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly ILogger<ResendEmailConfirmationModel> _logger;

    public ResendEmailConfirmationModel(
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        ILogger<ResendEmailConfirmationModel> logger)
    {
        _userManager = userManager;
        _emailService = emailService;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool Sent { get; private set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }

        var user = await _userManager.FindByEmailAsync(Input.Email);

        // Anti-enumeration: only actually send when an unconfirmed account exists,
        // but always show the same neutral confirmation to the caller.
        if (user is { } && !await _userManager.IsEmailConfirmedAsync(user))
        {
            try
            {
                await EmailConfirmationSender.SendAsync(_userManager, _emailService, Url, Request.Scheme, user);
                _logger.LogInformation("Resent email confirmation for user {UserId}.", user.Id);
            }
            catch (Exception ex)
            {
                // Delivery failures must not leak account state or fail the request.
                _logger.LogError(ex, "Failed to resend confirmation email for user {UserId}.", user.Id);
            }
        }
        else
        {
            _logger.LogInformation("Resend confirmation requested for a non-existent or already-confirmed email.");
        }

        Sent = true;
        return Page();
    }

    public class InputModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Invalid email address")]
        public string Email { get; set; } = null!;
    }
}
