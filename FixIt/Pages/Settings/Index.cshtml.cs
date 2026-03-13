using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Authorization;
using FixIt.Models.Users;
using FixIt.Models.Locations;
using FixIt.Data.Repository.Contracts;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FixIt.Pages.Settings;

[Authorize]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRepository<ApplicationUser> _userRepository;
    private readonly IRepository<City> _cityRepository;

    public new ApplicationUser? User { get; set; }
    public List<City> AvailableCities { get; set; } = new();
    public string? SelectedCityId { get; set; }
    public string SuccessMessage { get; set; } = "";
    public string ErrorMessage { get; set; } = "";

    public IndexModel(
        UserManager<ApplicationUser> userManager,
        IRepository<ApplicationUser> userRepository,
        IRepository<City> cityRepository)
    {
        _userManager = userManager;
        _userRepository = userRepository;
        _cityRepository = cityRepository;
    }

    public async Task OnGetAsync()
    {
        User = await _userManager.GetUserAsync(base.User);
        SelectedCityId = User?.PreferredCityId;
        var cities = await _cityRepository.FindAsync(c => true);
        AvailableCities = cities.ToList();
    }

    public async Task<IActionResult> OnPostSetCityAsync(string cityId)
    {
        try
        {
            var userId = base.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                ErrorMessage = "Unable to identify user.";
                return Page();
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                ErrorMessage = "User not found.";
                return Page();
            }

            user.PreferredCityId = cityId;
            await _userRepository.ReplaceAsync(userId, user);
            SelectedCityId = cityId;
            SuccessMessage = "City preference updated successfully!";
            var cities = await _cityRepository.FindAsync(c => true);
            AvailableCities = cities.ToList();
            User = user;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to update city: {ex.Message}";
        }

        return Page();
    }
}
