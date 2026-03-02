namespace NBNavApp.Common.Util;

internal static class MovingAverage
{
    static readonly Queue<double> samples = new();
    static double accumSum;
    public static int WindowSize { get; set; } = 20;

    public static double Compute(double newSample)
    {
        accumSum += newSample;
        samples.Enqueue(accumSum);

        if (samples.Count > WindowSize)
        { accumSum -= samples.Dequeue(); }

        return accumSum / samples.Count;
    }
}
