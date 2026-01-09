namespace NBNavApp.Common.Navigation;

internal class WrongWayDetector
{
    DateTime? suspectSince;

    public double MinSpeedMps { get; set; } = 2.0;
    public double MaxPerpMeters { get; set; } = 35.0;
    public double AngleThresholdDeg { get; set; } = 100.0;
    public TimeSpan ConfirmTime = TimeSpan.FromSeconds(8);

    public bool Update(double speedMps, double dPerp, double angleDiffDeg, DateTime now)
    {
        if (speedMps < MinSpeedMps || dPerp > MaxPerpMeters)
        {
            suspectSince = null;
            return false;
        }
        
        if (angleDiffDeg > AngleThresholdDeg)
        {
            suspectSince ??= now;
            bool isWrongWay = (now - suspectSince.Value) >= ConfirmTime;
            return isWrongWay;
        }

        suspectSince = null;
        return false;
    }
}
