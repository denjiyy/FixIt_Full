namespace FixIt.Mobile.Controls;

public partial class EmptyStateView : ContentView
{
    public static readonly BindableProperty IconSourceProperty = BindableProperty.Create(
        nameof(IconSource),
        typeof(ImageSource),
        typeof(EmptyStateView),
        propertyChanged: (b, _, n) => ((EmptyStateView)b).IconImage.Source = n as ImageSource);

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title),
        typeof(string),
        typeof(EmptyStateView),
        propertyChanged: (b, _, n) => ((EmptyStateView)b).TitleLabel.Text = n as string ?? string.Empty);

    public static readonly BindableProperty SubtitleProperty = BindableProperty.Create(
        nameof(Subtitle),
        typeof(string),
        typeof(EmptyStateView),
        propertyChanged: (b, _, n) =>
        {
            var view = (EmptyStateView)b;
            var text = n as string;
            view.SubtitleLabel.Text = text ?? string.Empty;
            view.SubtitleLabel.IsVisible = !string.IsNullOrWhiteSpace(text);
        });

    public EmptyStateView()
    {
        InitializeComponent();
    }

    public ImageSource? IconSource
    {
        get => (ImageSource?)GetValue(IconSourceProperty);
        set => SetValue(IconSourceProperty, value);
    }

    public string? Title
    {
        get => (string?)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Subtitle
    {
        get => (string?)GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }
}
