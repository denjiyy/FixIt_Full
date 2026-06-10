using CommunityToolkit.Mvvm.ComponentModel;

namespace FixIt.Mobile.Models;

// A selectable hazard category in the HazardMode report form. Key is the server
// HazardType enum name (e.g. "Pothole"); Label is the short caption shown on the chip.
public partial class HazardTypeOption : ObservableObject
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;

    [ObservableProperty]
    private bool _isSelected;
}
