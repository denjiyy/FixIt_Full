namespace FixIt.Mobile.Views;

public partial class SkeletonCardView : ContentView
{
    public SkeletonCardView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, EventArgs e)
    {
        var animation = new Animation(v => SkeletonRoot.Opacity = v, 0.4, 0.9);
        animation.Commit(this, "Shimmer", 16, 800, Easing.SinInOut, repeat: () => true);
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        this.AbortAnimation("Shimmer");
    }
}
