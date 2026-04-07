using Android.App;
using Android.Content.PM;
using NBNavApp.Common.Navigation;
using NBNavApp.Common.Services;

namespace NBNavApp.Platforms.Android;

[Activity(Theme = "@style/Maui.SplashTheme", MainLauncher = true, LaunchMode = LaunchMode.SingleTop, ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density, ScreenOrientation = ScreenOrientation.Portrait)]
public class MainActivity : MauiAppCompatActivity
{
    protected override void OnResume()
    {
        base.OnResume();

        var visibility = IPlatformApplication.Current?.Services.GetService<AppVisibilityService>();
        visibility?.IsInForeground = true;

        CheckServiceStop();
    }

    protected override void OnPause()
    {
        base.OnPause();

        var visibility = IPlatformApplication.Current?.Services.GetService<AppVisibilityService>();
        visibility?.IsInForeground = false;
    }

    private static void CheckServiceStop()
    {
        var navService = IPlatformApplication.Current?.Services.GetService<NavigationManager>();
        if (navService?.ServiceStopRequested == true)
        {
            PlatformLocationService.Stop();
            navService.ServiceStopRequested = false;
        }
    }

}