using AspNetCore.Identity.Mongo.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using FixIt.Models.Users;
using FixIt.Models.Enums;
using FixIt.Services.Constants;

namespace FixIt.Areas.Admin.Pages.Users;

[Authorize(Policy = PolicyNames.AdminOnly)]
public class IndexModel : PageModel
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<MongoRole> _roleManager;
    private readonly ILogger<IndexModel> _logger;

    public List<ApplicationUser> Users { get; set; } = new();
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 15;
    public int TotalUsers { get; set; }
    public int TotalPages { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? SearchTerm { get; set; }

    public IndexModel(UserManager<ApplicationUser> userManager, RoleManager<MongoRole> roleManager, ILogger<IndexModel> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    public Task OnGetAsync(int pageNumber = 1)
    {
        try
        {
            PageNumber = pageNumber;
            var allUsers = _userManager.Users.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(SearchTerm))
            {
                allUsers = allUsers.Where(u => 
                    u.UserName!.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    u.Email!.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase) ||
                    u.DisplayName.Contains(SearchTerm, StringComparison.OrdinalIgnoreCase)
                );
            }

            TotalUsers = allUsers.Count();
            TotalPages = (int)Math.Ceiling(TotalUsers / (double)PageSize);

            Users = allUsers
                .OrderByDescending(u => u.CreatedAt)
                .Skip((pageNumber - 1) * PageSize)
                .Take(PageSize)
                .ToList();

            _logger.LogInformation("Admin viewed users list");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading users");
        }
        return Task.CompletedTask;
    }

    public async Task<IActionResult> OnPostBanAsync(string userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            user.IsBanned = true;
            user.BannedReason = "Banned by admin";
            user.BannedAt = DateTime.UtcNow;

            var result = await _userManager.UpdateAsync(user);
            
            if (result.Succeeded)
            {
                _logger.LogWarning($"User {user.UserName} banned by admin");
                TempData["SuccessMessage"] = $"{user.UserName} has been banned.";
            }

            return RedirectToPage(new { pageNumber = PageNumber });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error banning user");
            TempData["ErrorMessage"] = "Error banning user";
            return RedirectToPage(new { pageNumber = PageNumber });
        }
    }

    public async Task<IActionResult> OnPostUnbanAsync(string userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            user.IsBanned = false;
            user.BannedReason = null;
            user.BannedAt = null;

            var result = await _userManager.UpdateAsync(user);
            
            if (result.Succeeded)
            {
                _logger.LogInformation($"User {user.UserName} unbanned by admin");
                TempData["SuccessMessage"] = $"{user.UserName} has been unbanned.";
            }

            return RedirectToPage(new { pageNumber = PageNumber });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unbanning user");
            TempData["ErrorMessage"] = "Error unbanning user";
            return RedirectToPage(new { pageNumber = PageNumber });
        }
    }

    public async Task<IActionResult> OnPostRestrictAsync(string userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            user.IsRestricted = true;
            user.RestrictedUntil = DateTime.UtcNow.AddDays(30);
            user.RestrictionReason = "Restricted by admin";

            var result = await _userManager.UpdateAsync(user);
            
            if (result.Succeeded)
            {
                _logger.LogWarning($"User {user.UserName} restricted by admin");
                TempData["SuccessMessage"] = $"{user.UserName} has been restricted for 30 days.";
            }

            return RedirectToPage(new { pageNumber = PageNumber });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restricting user");
            TempData["ErrorMessage"] = "Error restricting user";
            return RedirectToPage(new { pageNumber = PageNumber });
        }
    }

    public async Task<IActionResult> OnPostUnrestrictAsync(string userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            user.IsRestricted = false;
            user.RestrictedUntil = null;
            user.RestrictionReason = null;

            var result = await _userManager.UpdateAsync(user);
            
            if (result.Succeeded)
            {
                _logger.LogInformation($"User {user.UserName} unrestricted by admin");
                TempData["SuccessMessage"] = $"{user.UserName} restrictions have been removed.";
            }

            return RedirectToPage(new { pageNumber = PageNumber });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error unrestricting user");
            TempData["ErrorMessage"] = "Error unrestricting user";
            return RedirectToPage(new { pageNumber = PageNumber });
        }
    }

    public async Task<IActionResult> OnPostChangeRoleAsync(string userId, UserRole role)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            // Remove all existing roles
            var existingRoles = await _userManager.GetRolesAsync(user);
            if (existingRoles.Count > 0)
            {
                var removeResult = await _userManager.RemoveFromRolesAsync(user, existingRoles);
                if (!removeResult.Succeeded)
                {
                    _logger.LogWarning("Failed to remove existing roles from user {UserId}", user.Id);
                }
            }

            // Add new role based on UserRole enum
            string? newRole = role switch
            {
                UserRole.Admin => RoleNames.Admin,
                UserRole.Moderator => RoleNames.Moderator,
                _ => null // User role doesn't need Identity role entry
            };

            if (newRole != null)
            {
                // Ensure role exists in Identity role store
                if (!await _roleManager.RoleExistsAsync(newRole))
                {
                    await _roleManager.CreateAsync(new MongoRole(newRole));
                }

                var addResult = await _userManager.AddToRoleAsync(user, newRole);
                if (!addResult.Succeeded)
                {
                    _logger.LogWarning("Failed to add role {Role} to user {UserId}", newRole, user.Id);
                    TempData["ErrorMessage"] = "Failed to update user role";
                    return RedirectToPage(new { pageNumber = PageNumber });
                }
            }

            // Also update the user.Role property for backward compatibility
            user.Role = role;
            var updateResult = await _userManager.UpdateAsync(user);
            
            if (updateResult.Succeeded)
            {
                _logger.LogInformation($"User {user.UserName} role changed to {role}");
                TempData["SuccessMessage"] = $"{user.UserName} role has been changed to {role}.";
            }
            else
            {
                _logger.LogWarning("Failed to update user role property for user {UserId}", user.Id);
                TempData["ErrorMessage"] = "Role added but failed to update user profile";
            }

            return RedirectToPage(new { pageNumber = PageNumber });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error changing user role");
            TempData["ErrorMessage"] = "Error changing user role";
            return RedirectToPage(new { pageNumber = PageNumber });
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(string userId)
    {
        try
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return NotFound();

            var result = await _userManager.DeleteAsync(user);
            
            if (result.Succeeded)
            {
                _logger.LogInformation($"User {user.UserName} deleted by admin");
                TempData["SuccessMessage"] = $"{user.UserName} has been deleted.";
            }
            else
            {
                TempData["ErrorMessage"] = "Error deleting user";
            }

            return RedirectToPage(new { pageNumber = PageNumber });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting user");
            TempData["ErrorMessage"] = "Error deleting user";
            return RedirectToPage(new { pageNumber = PageNumber });
        }
    }
}
