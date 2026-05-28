using Foundation;
using UIKit;

namespace FixIt.Mobile.Platforms.iOS;

internal static class StatusBar
{
    public static void Apply(AppTheme theme)
    {
        var style = theme == AppTheme.Dark
            ? UIStatusBarStyle.LightContent
            : UIStatusBarStyle.DarkContent;

        if (NSThread.IsMain)
        {
            UIApplication.SharedApplication.SetStatusBarStyle(style, animated: false);
        }
        else
        {
            MainThread.BeginInvokeOnMainThread(() =>
                UIApplication.SharedApplication.SetStatusBarStyle(style, animated: false));
        }
    }
}
