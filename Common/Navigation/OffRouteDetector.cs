using NBNavApp.Common.Util;
namespace NBNavApp.Common.Navigation;

public class OffRouteDetector
{
    DateTime? suspectSince;
    DateTime lastReroute = DateTime.MinValue;

    bool isOffRoute;

    public double AccuracyFactor { get; set; } = 2.5;
    public TimeSpan ConfirmTime { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan Cooldown { get; set; } = TimeSpan.FromSeconds(15);

    public static event EventHandler? GoneOffRoute;
    public static event EventHandler? ReturnedOnRoute;

    public bool Update(double dPerpMeters, double gpsAccuracyMeters, double speedMps, DateTime now)
    {
        if (now - lastReroute < Cooldown)
        {
            suspectSince = null;
            return false;
        }

        // Calculate adaptive threshold based on speed (linear scaling)
        double speedKmh = speedMps * 3.6;
        double minDistance = Constants.OFFROUTE_MIN_DISTANCE_CITY + (speedKmh / Constants.MAX_SPEED_KMH) * (Constants.OFFROUTE_MIN_DISTANCE_HIGHWAY - Constants.OFFROUTE_MIN_DISTANCE_CITY);
        minDistance = Math.Clamp(minDistance, Constants.OFFROUTE_MIN_DISTANCE_CITY, Constants.OFFROUTE_MIN_DISTANCE_HIGHWAY);

        var threshold = Math.Max(minDistance, AccuracyFactor * Math.Max(5, gpsAccuracyMeters));

        if (dPerpMeters > threshold)
        {
            suspectSince ??= now;
            if (!isOffRoute)
            { GoneOffRoute?.Invoke(this, EventArgs.Empty); }
            isOffRoute = true;

            if (now - suspectSince > ConfirmTime)
            {
                lastReroute = now;
                suspectSince = null;
                return true;
            }
        }
        else
        { suspectSince = null; }

        if (isOffRoute && dPerpMeters <= threshold)
        {
            ReturnedOnRoute?.Invoke(this, EventArgs.Empty);
            isOffRoute = false;
        }

        return false;
    }
}
