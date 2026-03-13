using FixIt.Models.Gamification;
using FixIt.Models.Users;
using FixIt.Services.Gamification;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MongoDB.Bson;

namespace FixIt.Pages.User;

public class ProfileModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IReputationService _reputationService;

    public ApplicationUser? UserProfile { get; set; }
    public UserReputation? UserReputation { get; set; }

    public ProfileModel(
        UserManager<ApplicationUser> userManager,
        IReputationService reputationService)
    {
        _userManager = userManager;
        _reputationService = reputationService;
    }

    public async Task<IActionResult> OnGetAsync(string? userId)
    {
        // If no userId provided or userId is invalid (e.g., 'profile'), use current user or redirect
        if (string.IsNullOrEmpty(userId))
        {
            // If user is authenticated, show their own profile
            if (User?.Identity?.IsAuthenticated == true)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    userId = currentUser.Id.ToString();
                }
                else
                {
                    return RedirectToPage("/Index");
                }
            }
            else
            {
                return RedirectToPage("/Index");
            }
        }

        // Validate that userId is a valid MongoDB ObjectId
        if (!ObjectId.TryParse(userId, out _))
        {
            // Invalid ObjectId format, redirect to home
            return RedirectToPage("/Index");
        }

        // Try to find the user
        var userProfile = await _userManager.FindByIdAsync(userId);
        if (userProfile == null)
        {
            // User not found, redirect to home
            return RedirectToPage("/Index");
        }

        UserProfile = userProfile;
        UserReputation = await _reputationService.GetUserReputationAsync(userId);
        return Page();
    }
}

