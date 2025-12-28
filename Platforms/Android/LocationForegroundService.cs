#if ANDROID26_0_OR_GREATER
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using AndroidX.Core.App;
using Android.Gms.Location;
using System.Diagnostics;

namespace NBNavApp
{
    [Service]
    public class LocationForegroundService : Service
    {
        const int NOTIF_ID = 1001;
        const string CHANNEL_ID = "gps_channel";

        IFusedLocationProviderClient? fused;
        LocationCallback? callback;

        private void CreateNotificationChannel()
        {
            if (Build.VERSION.SdkInt < BuildVersionCodes.O)
            { return; }

            var channel = new NotificationChannel(
                CHANNEL_ID,
                "GPS Tracking",
                NotificationImportance.Low
            );
            var mgr = (NotificationManager)GetSystemService(NotificationService)!;
            mgr.CreateNotificationChannel(channel);
        }

        private Notification BuildNotification(string text)
        {
            var openIntent = new Intent(this, typeof(MainActivity));
            openIntent.AddFlags(ActivityFlags.SingleTop);

            var pending = PendingIntent.GetActivity(
                this, 0, openIntent,
                PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable
            );

            return new NotificationCompat.Builder(this, CHANNEL_ID)
                .SetContentTitle("NB Navigation")
                .SetContentText(text)
                .SetSmallIcon(Resource.Drawable.pin)
                .SetOngoing(true)
                .SetContentIntent(pending)
                .Build();
        }

        public override void OnCreate()
        {
            base.OnCreate();
            CreateNotificationChannel();

            fused = LocationServices.GetFusedLocationProviderClient(this);

            callback = new NBLocationCallback(loc =>
            {
                System.Diagnostics.Debug.WriteLine($"GPS: {loc.Latitude},{loc.Longitude} acc={loc.Accuracy}");
            });
        }

        public override IBinder? OnBind(Intent? intent) => null;

        public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
        {
            StartForeground(NOTIF_ID, BuildNotification("GPS active"));

            StartLocationUpdates();
            return StartCommandResult.Sticky;
        }

        public override void OnDestroy()
        {
            StopLocationUpdates();
            base.OnDestroy();
        }

        private void StartLocationUpdates()
        {
            if (fused == null || callback == null)
            { return; }

            var req = new LocationRequest.Builder(Priority.PriorityHighAccuracy, 5000)
                .SetMinUpdateIntervalMillis(2000)
                .SetWaitForAccurateLocation(false)
                .Build();

            fused.RequestLocationUpdates(req, callback, Looper.MainLooper);
        }

        private void StopLocationUpdates()
        {
            if (fused != null && callback != null)
            { fused.RemoveLocationUpdates(callback); }
        }
    }



    class NBLocationCallback : LocationCallback
    {
        readonly Action<Android.Locations.Location> onLocation;

        public NBLocationCallback(Action<Android.Locations.Location> onLocation) => this.onLocation = onLocation;

        public override void OnLocationResult(LocationResult result)
        {
            var loc = result.LastLocation;
            if (loc != null)
            { onLocation(loc); }
        }
    }
}
#endif
