namespace NBNavApp.Common.Util;

internal class MovingAverage
{
    Queue<double> samples = new();
    private double accumSum;
    public int WindowSize { get; set; } = 20;

    public double Compute(double newSample)
    {
        accumSum += newSample;
        samples.Enqueue(accumSum);

        if (samples.Count > WindowSize)
        { accumSum -= samples.Dequeue(); }

        return accumSum / samples.Count;
    }
}
