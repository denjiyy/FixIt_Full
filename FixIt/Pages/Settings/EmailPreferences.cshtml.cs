using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using FixIt.Models.Users;
using FixIt.Models.Locations;
using FixIt.Data.Repository.Contracts;
using Microsoft.AspNetCore.Identity;

namespace FixIt.Pages.Settings;

[Authorize]
public class EmailPreferencesModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRepository<City> _cityRepository;

    public new ApplicationUser? User { get; set; }
    public List<City> AvailableCities { get; set; } = new();
    public string? SuccessMessage { get; set; }

    public EmailPreferencesModel(
        UserManager<ApplicationUser> userManager,
        IRepository<City> cityRepository)
    {
        _userManager = userManager;
        _cityRepository = cityRepository;
    }

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await _userManager.GetUserAsync(base.User);
        if (user == null)
            return NotFound();

        this.User = user;

        // Load available cities
        var cities = await _cityRepository.FindAsync(_ => true);
        AvailableCities = cities.OrderBy(c => c.Name).ToList();

        return Page();
    }

    public async Task<IActionResult> OnPostUpdateAllNotificationsAsync(bool emailNotificationsEnabled)
    {
        var user = await _userManager.GetUserAsync(base.User);
        if (user == null)
            return NotFound();

        user.EmailNotificationsEnabled = emailNotificationsEnabled;
        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "Failed to update notifications");
            return Page();
        }

        SuccessMessage = emailNotificationsEnabled 
            ? "Email notifications enabled" 
            : "Email notifications disabled";

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostUpdatePreferencesAsync(
        string? preferredCityId,
        bool receiveHealthReports,
        bool receiveWeeklyReminders,
        bool receiveHazardAlerts)
    {
        var user = await _userManager.GetUserAsync(base.User);
        if (user == null)
            return NotFound();

        user.PreferredCityId = string.IsNullOrEmpty(preferredCityId) ? null : preferredCityId;
        user.ReceiveHealthReports = receiveHealthReports;
        user.ReceiveWeeklyReminders = receiveWeeklyReminders;
        user.ReceiveHazardAlerts = receiveHazardAlerts;

        var result = await _userManager.UpdateAsync(user);

        if (!result.Succeeded)
        {
            ModelState.AddModelError("", "Failed to update preferences");
            return Page();
        }

        SuccessMessage = "Email preferences updated successfully";

        return RedirectToPage();
    }
}
