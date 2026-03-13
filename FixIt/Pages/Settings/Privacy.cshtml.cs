using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FixIt.Models.Users;
using FixIt.Data.Repository.Contracts;
using System.Security.Claims;

namespace FixIt.Pages.Settings;

[Authorize]
public class PrivacyModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ApplicationUser? CurrentUser { get; set; }
    public AlertPreferences? AlertPreferences { get; set; }
    public string ProfileVisibility { get; set; } = "public";

    public PrivacyModel(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task OnGetAsync()
    {
        CurrentUser = await _userManager.GetUserAsync(User);
        
        // Load alert preferences (stored in user metadata or separate collection)
        AlertPreferences = new AlertPreferences
        {
            CrimeAlerts = true,
            AccidentAlerts = true,
            InfrastructureAlerts = true,
            AllHazards = false,
            AlertRadiusKm = 5,
            SeverityThreshold = "All"
        };
    }
}

public class AlertPreferences
{
    public bool CrimeAlerts { get; set; }
    public bool AccidentAlerts { get; set; }
    public bool InfrastructureAlerts { get; set; }
    public bool AllHazards { get; set; }
    public int AlertRadiusKm { get; set; }
    public string SeverityThreshold { get; set; } = "All";
}
