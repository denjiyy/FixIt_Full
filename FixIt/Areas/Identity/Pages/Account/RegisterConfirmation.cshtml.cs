using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace FixIt.Areas.Identity.Pages.Account;

public class RegisterConfirmationModel : PageModel
{
    public string? Email { get; set; }

    public IActionResult OnGet(string? email = null)
    {
        if (email == null)
            return RedirectToPage("./Register");

        Email = email;
        return Page();
    }
}
