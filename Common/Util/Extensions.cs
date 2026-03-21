namespace NBNavApp;

public static class Extensions
{
    public static double ToRad(this double d) => d * Math.PI / 180;
    public static double ToDeg(this double r) => r * 180 / Math.PI;
    public static int RoundToTens(this int i) => ((int)Math.Round(i / 10.0)) * 10;
    public static int RoundToHundreds(this int i) => ((int)Math.Round(i / 100.0)) * 100;
}
