namespace NBNavApp.Common.Navigation;

public class OffRouteDetector
{
    DateTime? suspectSince;
    DateTime lastReroute = DateTime.MinValue;

    bool isOffRoute;

    public double MinDistanceMeters { get; set; } = 150;
    public double AccuracyFactor { get; set; } = 2.5;
    public TimeSpan ConfirmTime { get; set; } = TimeSpan.FromSeconds(5);
    public TimeSpan Cooldown { get; set; } = TimeSpan.FromSeconds(15);

    public static event EventHandler? GoneOffRoute;
    public static event EventHandler? ReturnedOnRoute;

    public bool Update(double dPerpMeters, double gpsAccuracyMeters, DateTime now)
    {
        if (now - lastReroute < Cooldown)
        {
            suspectSince = null;
            return false;
        }

        var threshold = Math.Max(MinDistanceMeters, AccuracyFactor * Math.Max(5, gpsAccuracyMeters));

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
