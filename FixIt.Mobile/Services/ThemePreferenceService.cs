using FixIt.Mobile.Services.Contracts;

namespace FixIt.Mobile.Services;

public sealed class ThemePreferenceService : IThemePreferenceService
{
    private const string PreferenceKey = "fixit.app_theme";

    public AppThemePreference GetPreference()
    {
        var stored = Preferences.Default.Get(PreferenceKey, nameof(AppThemePreference.System));
        return Enum.TryParse<AppThemePreference>(stored, ignoreCase: true, out var preference)
            ? preference
            : AppThemePreference.System;
    }

    public void SetPreference(AppThemePreference preference)
    {
        Preferences.Default.Set(PreferenceKey, preference.ToString());
        Apply();
    }

    public void Apply()
    {
        if (Application.Current is not { } app)
        {
            return;
        }

        var target = GetPreference() switch
        {
            AppThemePreference.Light => AppTheme.Light,
            AppThemePreference.Dark => AppTheme.Dark,
            _ => AppTheme.Unspecified,
        };

        MainThread.BeginInvokeOnMainThread(() =>
        {
            app.UserAppTheme = target;
#if IOS
            var effective = target == AppTheme.Unspecified
                ? app.RequestedTheme
                : target;
            FixIt.Mobile.Platforms.iOS.StatusBar.Apply(effective);
#endif
        });
    }
}
