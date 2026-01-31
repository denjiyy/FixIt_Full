using FixIt.Models.Gamification;
using FixIt.Models.Users;
using FixIt.Services.Gamification;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FixIt.Pages.User;

public class ProfileModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IReputationService _reputationService;

    public ApplicationUser? User { get; set; }
    public UserReputation? UserReputation { get; set; }

    public ProfileModel(
        UserManager<ApplicationUser> userManager,
        IReputationService reputationService)
    {
        _userManager = userManager;
        _reputationService = reputationService;
    }

    public async Task OnGetAsync(string userId)
    {
        User = await _userManager.FindByIdAsync(userId);
        if (User != null)
        {
            UserReputation = await _reputationService.GetUserReputationAsync(userId);
        }
    }
}
