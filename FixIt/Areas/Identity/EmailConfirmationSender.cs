using System.Text;
using System.Text.Encodings.Web;
using FixIt.Models.Users;
using FixIt.Services.Email;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;

namespace FixIt.Areas.Identity;

/// <summary>
/// Single source of truth for issuing an email-confirmation link. Shared by the
/// web registration page, the resend page, and the mobile/API register endpoint
/// so the token format and callback target never drift apart.
/// </summary>
public static class EmailConfirmationSender
{
    /// <summary>
    /// Generates a confirmation token for <paramref name="user"/> and emails an
    /// absolute link to the ConfirmEmail page. Returns the callback URL (handy for
    /// logging in dev, where the Console email provider only logs a preview).
    /// </summary>
    public static async Task<string?> SendAsync(
        UserManager<ApplicationUser> userManager,
        IEmailService emailService,
        IUrlHelper url,
        string requestScheme,
        ApplicationUser user)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
        {
            return null;
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        var callbackUrl = url.Page(
            "/Account/ConfirmEmail",
            pageHandler: null,
            values: new { area = "Identity", userId = user.Id.ToString(), code },
            protocol: requestScheme);

        await emailService.SendEmailAsync(
            user.Email,
            "Confirm your FixIt email",
            $"Welcome to FixIt! Please confirm your email by " +
            $"<a href='{HtmlEncoder.Default.Encode(callbackUrl!)}'>clicking here</a>. " +
            "If you didn't create this account you can ignore this message.");

        return callbackUrl;
    }
}
