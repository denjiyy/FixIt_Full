using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Localization;

namespace FixIt.Pages
{
    public class SetLanguageModel : PageModel
    {
        public IActionResult OnGet(string culture, string returnUrl = "/")
        {
            if (string.IsNullOrEmpty(culture))
            {
                culture = "en-US";
            }

            var supportedCultures = new[] { "en-US", "bg-BG" };
            if (!supportedCultures.Contains(culture))
            {
                culture = "en-US";
            }

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1), IsEssential = true }
            );

            if (string.IsNullOrEmpty(returnUrl) || !Url.IsLocalUrl(returnUrl))
            {
                returnUrl = "/";
            }

            return LocalRedirect(returnUrl);
        }
    }
}
