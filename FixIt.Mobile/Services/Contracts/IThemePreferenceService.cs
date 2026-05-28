namespace FixIt.Mobile.Services.Contracts;

public enum AppThemePreference
{
    System,
    Light,
    Dark,
}

public interface IThemePreferenceService
{
    AppThemePreference GetPreference();

    void SetPreference(AppThemePreference preference);

    void Apply();
}
