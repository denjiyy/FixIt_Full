using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace FixIt.Mobile.Localization;

public sealed class LocalizationService : INotifyPropertyChanged
{
    private static readonly CultureInfo EnglishCulture = new("en-US");
    private static readonly CultureInfo BulgarianCulture = new("bg-BG");
    private static readonly HashSet<string> SupportedCultures = new(StringComparer.OrdinalIgnoreCase)
    {
        EnglishCulture.Name,
        BulgarianCulture.Name
    };

    private readonly ResourceManager _resourceManager = new(
        "FixIt.Mobile.Resources.Strings.AppStrings",
        typeof(LocalizationService).Assembly);

    public static LocalizationService Instance { get; } = new();

    private LocalizationService()
    {
        CurrentCulture = NormalizeCulture(CultureInfo.CurrentUICulture);
        ApplyCulture(CurrentCulture);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public static event EventHandler<CultureInfo>? CultureChanged;

    public CultureInfo CurrentCulture { get; private set; }

    public string this[string key] => Get(key);

    public static string Get(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return string.Empty;
        }

        return Instance._resourceManager.GetString(key, Instance.CurrentCulture)
            ?? Instance._resourceManager.GetString(key, EnglishCulture)
            ?? key;
    }

    public static void SetCulture(string cultureName)
    {
        Instance.ChangeCulture(NormalizeCulture(new CultureInfo(cultureName)));
    }

    public static void UseDeviceCulture()
    {
        Instance.ChangeCulture(NormalizeCulture(CultureInfo.CurrentUICulture));
    }

    private void ChangeCulture(CultureInfo culture)
    {
        if (CurrentCulture.Name.Equals(culture.Name, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        CurrentCulture = culture;
        ApplyCulture(culture);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        CultureChanged?.Invoke(this, culture);
    }

    private static CultureInfo NormalizeCulture(CultureInfo culture)
    {
        if (SupportedCultures.Contains(culture.Name))
        {
            return culture.Name.Equals(BulgarianCulture.Name, StringComparison.OrdinalIgnoreCase)
                ? BulgarianCulture
                : EnglishCulture;
        }

        if (culture.TwoLetterISOLanguageName.Equals("bg", StringComparison.OrdinalIgnoreCase))
        {
            return BulgarianCulture;
        }

        return EnglishCulture;
    }

    private static void ApplyCulture(CultureInfo culture)
    {
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;
    }
}
