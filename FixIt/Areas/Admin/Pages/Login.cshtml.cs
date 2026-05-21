using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using FixIt.Models.Users;
using FixIt.Models.Enums;
using FixIt.Services.Constants;

namespace FixIt.Areas.Admin.Pages;

[EnableRateLimiting(RateLimitPolicyNames.AuthStrict)]
public class LoginModel : PageModel
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<LoginModel> _logger;

    public LoginModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ILogger<LoginModel> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _logger = logger;
    }

    [BindProperty]
    public LoginInputModel Input { get; set; } = new();

    [TempData]
    public string? ErrorMessage { get; set; }

    public string? ReturnUrl { get; set; }

    public async Task OnGetAsync(string? returnUrl = null)
    {
        ErrorMessage = null;
        ReturnUrl = returnUrl ?? "/admin/dashboard";

        // Clear the existing external cookie to ensure a clean login process
        await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        returnUrl ??= "/admin/dashboard";

        if (ModelState.IsValid)
        {
            // Find the user
            var user = await _userManager.FindByNameAsync(Input.Username);
            if (user == null)
            {
                _logger.LogWarning($"Login attempt with unknown username: {Input.Username}");
                ModelState.AddModelError(string.Empty, "Invalid username or password");
                return Page();
            }

            // Admin area access is restricted to administrators and moderators.
            if (user.Role != UserRole.Admin && user.Role != UserRole.Moderator)
            {
                _logger.LogWarning($"Unauthorized admin area login attempt: {Input.Username} (Role: {user.Role})");
                ModelState.AddModelError(string.Empty, "You don't have permission to access the admin panel");
                return Page();
            }

            // Attempt to sign in
            var result = await _signInManager.PasswordSignInAsync(Input.Username, Input.Password, Input.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation($"Admin {user.Email} ({user.Role}) signed in");
                var destination = user.Role == UserRole.Moderator ? "/admin/issues" : returnUrl;
                return LocalRedirect(destination);
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning($"User account locked out: {Input.Username}");
                ModelState.AddModelError(string.Empty, "Account locked out. Please try again later");
                return Page();
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt");
            return Page();
        }

        return Page();
    }

    public class LoginInputModel
    {
        [Required(ErrorMessage = "Username is required")]
        [Display(Name = "Username")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me?")]
        public bool RememberMe { get; set; }
    }
}
