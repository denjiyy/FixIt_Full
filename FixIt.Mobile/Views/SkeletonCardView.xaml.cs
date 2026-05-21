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
        this.AbortAnimation("Shimmer");
        var animation = new Animation(v => SkeletonRoot.Opacity = v, 0.4, 0.9);
        animation.Commit(this, "Shimmer", 16, 800, Easing.SinInOut, repeat: () => true);
    }

    private void OnUnloaded(object? sender, EventArgs e)
    {
        try
        {
            // FIX B-05: stop recycled shimmer animations before iOS detaches the native view.
            this.AbortAnimation("Shimmer");
        }
        catch (ObjectDisposedException ex)
        {
            Console.WriteLine($"[SkeletonCard] Animation abort skipped: {ex.Message}");
        }
    }
}
