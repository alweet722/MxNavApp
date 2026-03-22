namespace NBNavApp;

public class PlatformLocationService
{
    public static async Task StartAsync()
    {
        var status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        if (status != PermissionStatus.Granted)
        { throw new InvalidOperationException("Location permission denied"); }
#if ANDROID31_0_OR_GREATER
        var ctx = Android.App.Application.Context;
        var intent = new Android.Content.Intent(ctx, typeof(LocationForegroundService));
        ctx.StartForegroundService(intent);
#endif
    }

    public static void Stop()
    {
#if ANDROID31_0_OR_GREATER
        var ctx = Android.App.Application.Context;
        var intent = new Android.Content.Intent(ctx, typeof(LocationForegroundService));
        intent.SetAction("STOP_LOCATION");
        ctx.StartService(intent);
#endif
    }
}