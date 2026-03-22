#if ANDROID31_0_OR_GREATER
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Gms.Location;
using Android.OS;
using AndroidX.Core.App;
using NBNavApp.Platforms.Android;

namespace NBNavApp;

[Service(Exported = false, ForegroundServiceType = ForegroundService.TypeLocation)]
public class LocationForegroundService : Service
{
    const int NOTIF_ID = 1001;
    const string CHANNEL_ID = "gps_channel";
    const string ACTION_STOP = "STOP_LOCATION";

    IFusedLocationProviderClient? fused;

    PendingIntent? pendingIntent;

    private Notification BuildNotification(string text)
    {
        var openIntent = new Intent(this, typeof(MainActivity));
        openIntent.AddFlags(ActivityFlags.SingleTop);

        var pending = PendingIntent.GetActivity(
            this, 0, openIntent,
            PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Mutable
        );

        return new NotificationCompat.Builder(this, CHANNEL_ID)
            .SetContentTitle("NB Navigation")
            .SetContentText(text)
            .SetSmallIcon(Android.Resource.Drawable.IcMenuCompass)
            .SetOngoing(true)
            .SetContentIntent(pending)
            .Build();
    }

    public override void OnCreate()
    {
        base.OnCreate();
        CreateNotificationChannel();

        fused = LocationServices.GetFusedLocationProviderClient(this);
    }

    public override IBinder? OnBind(Intent? intent) => null;

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var action = intent?.Action;
        if (action == ACTION_STOP)
        {
            StopLocationUpdates();
            StopForeground(StopForegroundFlags.Remove);
            StopSelf();

            return StartCommandResult.NotSticky;
        }

        var notification = BuildNotification("Navigation active");

        ServiceCompat.StartForeground(
            this,
            NOTIF_ID,
            notification,
            (int)ForegroundService.TypeLocation
        );
        StartLocationUpdates();
        return StartCommandResult.NotSticky;
    }

    public override void OnDestroy()
    {
        StopLocationUpdates();
        StopForeground(StopForegroundFlags.Remove);
        StopSelf();
        base.OnDestroy();
    }

    private PendingIntent GetLocationPendingIntent()
    {
        var intent = new Intent(this, typeof(LocationUpdatesReceiver));
        intent.SetAction(LocationUpdatesReceiver.ActionProcessUpdates);

        var flags = PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Mutable;
        return PendingIntent.GetBroadcast(this, 0, intent, flags)!;
    }

    private void CreateNotificationChannel()
    {
        var channel = new NotificationChannel(
           CHANNEL_ID,
           "GPS Tracking",
           NotificationImportance.Low
       );
        var mgr = (NotificationManager)GetSystemService(NotificationService)!;
        mgr.CreateNotificationChannel(channel);
    }

    private void StartLocationUpdates()
    {
        if (fused == null)
        { return; }

        pendingIntent ??= GetLocationPendingIntent();

        var req = new LocationRequest.Builder(Priority.PriorityHighAccuracy, 1000)
            .SetMinUpdateIntervalMillis(1000)
            .SetWaitForAccurateLocation(false)
            .Build();

        fused.RequestLocationUpdates(req, pendingIntent);
    }

    private void StopLocationUpdates()
    {
        if (fused == null)
        { return; }

        if (pendingIntent != null)
        { fused.RemoveLocationUpdates(pendingIntent); }

        pendingIntent?.Cancel();
        pendingIntent = null;
    }
}
#endif