#if ANDROID31_0_OR_GREATER
using Android.Content;
using Android.Gms.Location;
using NBNavApp.Common.Navigation;

namespace NBNavApp.Platforms.Android;

[BroadcastReceiver(Enabled = true, Exported = false)]
public class LocationUpdatesReceiver : BroadcastReceiver
{
    public const string ActionProcessUpdates = "NBNavApp.LOCATION_UPDATES";

    public override void OnReceive(Context? context, Intent? intent)
    {
        if (intent?.Action != ActionProcessUpdates)
        { return; }

        var result = LocationResult.ExtractResult(intent);
        var location = result?.LastLocation;
        if (location == null)
        { return; }

        LocationBus.Publish(location);
    }
}
#endif