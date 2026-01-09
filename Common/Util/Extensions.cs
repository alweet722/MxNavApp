namespace NBNavApp;

public static class Extensions
{
    public static double ToRad(this double d) => d * Math.PI / 180;
    public static double ToDeg(this double r) => r * 180 / Math.PI;
}
