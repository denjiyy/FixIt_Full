using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using FixIt.Mobile.Constants;
using FixIt.Mobile.Localization;
using FixIt.Mobile.Models;
using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.Services;

/// <summary>
/// Centralised handler that maps <see cref="ApiResult"/> failures to user-visible
/// toasts (or alerts) and shared navigation actions for auth failures. Mirrors
/// the web error-state spec (spec §5.4):
///   401 -> sign out + navigate to login
///   403 -> "You don't have permission" toast
///   404 -> caller handles inline empty state
///   429 -> "Too many requests" toast
///   500+ -> generic "Something went wrong" toast + logged error
/// </summary>
public static class ApiErrorHandler
{
    public static async Task HandleAsync(ApiResult result, IAuthService? auth = null, CancellationToken ct = default)
    {
        if (result.Success)
        {
            return;
        }

        var status = result.HttpStatus ?? 0;
        string message;

        switch (status)
        {
            case 401:
                message = LocalizationService.Get("Common_Error_Unauthorized");
                if (auth != null)
                {
                    try { await auth.LogoutAsync(ct); } catch { /* ignore */ }
                }
                await SafeShowAsync(message);
                try
                {
                    if (Shell.Current != null)
                    {
                        await Shell.Current.GoToAsync(AppConstants.RouteAccountTabAbsolute);
                    }
                }
                catch { /* ignore */ }
                return;
            case 403:
                message = LocalizationService.Get("Common_Error_Forbidden");
                break;
            case 404:
                // Caller renders inline empty state — no toast required.
                return;
            case 429:
                message = LocalizationService.Get("Common_Error_TooManyRequests");
                break;
            case >= 500:
                message = LocalizationService.Get("Common_Error_Generic");
                Console.WriteLine($"[API ERROR] status={status} err={result.Error}");
                break;
            default:
                message = !string.IsNullOrWhiteSpace(result.Error)
                    ? result.Error!
                    : LocalizationService.Get("Common_Error_Generic");
                break;
        }

        await SafeShowAsync(message);
    }

    public static Task ShowAsync(string message)
    {
        return SafeShowAsync(message);
    }

    private static async Task SafeShowAsync(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        try
        {
            var toast = Toast.Make(message, ToastDuration.Short, 14);
            await toast.Show();
        }
        catch
        {
            // Toast can fail on simulator or if main thread isn't ready — fall back to a
            // DisplayAlert when possible so the user still sees the message.
            try
            {
                if (Shell.Current != null)
                {
                    await Shell.Current.DisplayAlert(LocalizationService.Get("Common_Error_Generic"), message, LocalizationService.Get("Common_OK"));
                }
            }
            catch { /* swallow — non-fatal */ }
        }
    }
}
