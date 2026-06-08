using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using FixIt.Models.Users;
using FixIt.Models.Enums;
using FixIt.Services.Constants;

namespace FixIt.Areas.Identity.Pages.Account;

[IgnoreAntiforgeryToken(Order = 1000)]
[EnableRateLimiting(RateLimitPolicyNames.AuthStrict)]
public class ExternalLoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ExternalLoginModel> _logger;

    public ExternalLoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ILogger<ExternalLoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public InputModel Input { get; set; } = default!;

    public string? LoginProvider { get; set; }

    public string? ReturnUrl { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnGetCallbackAsync(string? returnUrl = null, string? remoteError = null)
    {
        // Only ever redirect to a local URL; fall back to the site root otherwise.
        returnUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Content("~/");

        if (remoteError != null)
        {
            // Surface the provider error on the login page (a redirect drops
            // ModelState, so we carry the message via TempData instead).
            _logger.LogWarning("External provider returned an error: {RemoteError}", remoteError);
            ErrorMessage = $"Error from external provider: {remoteError}";
            return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
        }

        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            ErrorMessage = "Error loading external login information.";
            return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
        }

        // Sign in the user with this external login provider if the user already has a login.
        var result = await _signInManager.ExternalLoginSignInAsync(info.LoginProvider, info.ProviderKey, isPersistent: false, bypassTwoFactor: true);
        if (result.Succeeded)
        {
            _logger.LogInformation("{Name} logged in with {LoginProvider} provider.", info.Principal.Identity?.Name, info.LoginProvider);
            return LocalRedirect(returnUrl);
        }
        if (result.IsLockedOut)
        {
            return RedirectToPage("./Lockout");
        }

        ReturnUrl = returnUrl;
        LoginProvider = info.LoginProvider;

        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        var providerName = info.Principal.FindFirstValue(ClaimTypes.Name);

        // If a local account already exists for this (provider-verified) email but
        // isn't yet linked to this external login, link them and sign in instead of
        // dead-ending. Without this, a user who first registered with a password
        // (or via the mobile OAuth path) hits "email already taken" when they click
        // "Sign in with Google" — the create below would fail. Google verifies email
        // ownership, so linking by email is safe and matches the API OAuth flow.
        if (!string.IsNullOrWhiteSpace(email))
        {
            var existingUser = await _userManager.FindByEmailAsync(email);
            if (existingUser != null)
            {
                var linkResult = await _userManager.AddLoginAsync(existingUser, info);
                if (linkResult.Succeeded)
                {
                    MirrorExternalIdentity(existingUser, info, providerName ?? existingUser.DisplayName);
                    if (!existingUser.EmailConfirmed)
                    {
                        existingUser.EmailConfirmed = true;
                    }
                    await _userManager.UpdateAsync(existingUser);

                    await _signInManager.SignInAsync(existingUser, isPersistent: false);
                    _logger.LogInformation(
                        "Linked {LoginProvider} login to existing account {UserId} by verified email and signed in.",
                        info.LoginProvider, existingUser.Id);
                    return LocalRedirect(returnUrl);
                }

                // Linking failed for an unexpected reason — surface it rather than
                // falling through to a create that will also fail on the unique email.
                _logger.LogWarning(
                    "Failed to link {LoginProvider} login to existing account {UserId}: {Errors}",
                    info.LoginProvider, existingUser.Id,
                    string.Join("; ", linkResult.Errors.Select(e => e.Description)));
                ErrorMessage = "We couldn't connect your account. Please sign in with your password and link the provider from settings.";
                return RedirectToPage("./Login", new { ReturnUrl = returnUrl });
            }
        }

        // Otherwise, ask the user to confirm creating a new account.
        // Choose a safe, non-empty default display name from provider claims/email.
        var defaultDisplayName = !string.IsNullOrWhiteSpace(providerName)
            ? providerName
            : !string.IsNullOrWhiteSpace(email)
                ? email.Split('@')[0]
                : $"{info.LoginProvider} user";

        Input = new InputModel
        {
            Email = email ?? string.Empty,
            DisplayName = defaultDisplayName
        };
        return Page();
    }

    public Task<IActionResult> OnPostAsync(string provider, string? returnUrl = null)
    {
        // Request a redirect to the external login provider
        var redirectUrl = Url.Page("./ExternalLogin", pageHandler: "Callback", values: new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(provider, redirectUrl);
        return Task.FromResult<IActionResult>(new ChallengeResult(provider, properties));
    }

    public async Task<IActionResult> OnPostConfirmationAsync(string? returnUrl = null)
    {
        returnUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? returnUrl
            : Url.Content("~/");

        // Get the information about the user from the external login provider
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            ModelState.AddModelError(string.Empty, "An error occurred loading external login information.");
            return Page();
        }

        if (ModelState.IsValid)
        {
            var user = new ApplicationUser
            {
                UserName = Input.Email,
                Email = Input.Email,
                DisplayName = Input.DisplayName,
                Role = UserRole.User,
                // The provider (Google) has verified ownership of this email, so the
                // account is confirmed from the start and won't be blocked if
                // SignIn.RequireConfirmedEmail is enabled.
                EmailConfirmed = true,
            };

            var result = await _userManager.CreateAsync(user);
            if (result.Succeeded)
            {
                result = await _userManager.AddLoginAsync(user, info);
                if (result.Succeeded)
                {
                    // Mirror the Identity external login into
                    // ApplicationUser.ExternalIdentities so the connected-accounts
                    // UI reflects the linked provider accurately.
                    MirrorExternalIdentity(user, info, Input.DisplayName);
                    await _userManager.UpdateAsync(user);

                    _logger.LogInformation("User created an account using {Name} provider.", info.LoginProvider);

                    // If account confirmation is required, we need to show the link if we don't have a real email sender
                    if (_userManager.Options.SignIn.RequireConfirmedEmail)
                    {
                        return RedirectToPage("./RegisterConfirmation", new { Email = Input.Email });
                    }

                    await _signInManager.SignInAsync(user, isPersistent: false);
                    return LocalRedirect(returnUrl);
                }
            }
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
        }

        LoginProvider = info.LoginProvider;
        ReturnUrl = returnUrl;
        return Page();
    }

    // Keep ApplicationUser.ExternalIdentities in sync with the Identity login store
    // so the connected-accounts UI (and the mobile OAuth path) see the same links.
    private static void MirrorExternalIdentity(ApplicationUser user, ExternalLoginInfo info, string displayName)
    {
        if (user.ExternalIdentities.Any(e =>
                e.Provider == info.LoginProvider && e.ProviderId == info.ProviderKey))
        {
            return;
        }

        user.ExternalIdentities.Add(new ExternalIdentity
        {
            Provider = info.LoginProvider,
            ProviderId = info.ProviderKey,
            ProviderDisplayName = displayName,
            ConnectedAt = DateTime.UtcNow,
            LastSignInAt = DateTime.UtcNow
        });
    }

    public class InputModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Display name is required")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Display name must be between 2 and 100 characters")]
        public string DisplayName { get; set; } = null!;
    }
}
