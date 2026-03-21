namespace NBNavApp.Common.Navigation;

internal class WrongWayDetector
{
    DateTime? suspectSince;

    bool suspectWrongWay;

    public double MinSpeedMps { get; set; } = 2.0;
    public double MaxPerpMeters { get; set; } = 100;
    public double AngleThresholdDeg { get; set; } = 100.0;
    public TimeSpan DelayTime = TimeSpan.FromSeconds(30);
    public TimeSpan Cooldown { get; set; } = TimeSpan.FromSeconds(30);

    public static event EventHandler? GoingWrongWay;
    public static event EventHandler? Turned;

    public bool Update(double angleDiffDeg, DateTime now)
    {
        //if (speedMps < MinSpeedMps || dPerp > MaxPerpMeters)
        //{
        //    suspectSince = null;
        //    return false;
        //}

        if (angleDiffDeg > AngleThresholdDeg)
        {
            suspectSince ??= now;
            if (!suspectWrongWay)
            { GoingWrongWay?.Invoke(this, EventArgs.Empty); }
            suspectWrongWay = true;
        }
        else
        { suspectSince = null; }

        if (suspectWrongWay && angleDiffDeg <= AngleThresholdDeg)
        {
            Turned?.Invoke(this, EventArgs.Empty);
            suspectWrongWay = false;
        }

        return suspectWrongWay;
    }

    public bool UpdateReroute(DateTime now)
    {
        if (suspectSince == null)
        { return false; }

        if (now - suspectSince.Value > DelayTime)
        {
            suspectSince = null;
            return true;
        }

        return false;
    }
}
