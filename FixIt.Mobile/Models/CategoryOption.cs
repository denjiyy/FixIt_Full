using CommunityToolkit.Mvvm.ComponentModel;

namespace FixIt.Mobile.Models;

// A selectable tile in the Report screen's category grid. Key feeds the server
// category, Label is the short tile caption, IconSource is the cat_*.png glyph.
public partial class CategoryOption : ObservableObject
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string IconSource { get; init; } = "feed_hazard.png";

    [ObservableProperty]
    private bool _isSelected;
}
