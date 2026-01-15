using Mapsui.Projections;

namespace NBNavApp.Common.Navigation;

internal class GeoFunctions
{
    public static double HaversineMeters((double lon, double lat) start, (double lon, double lat) end)
    {
        const double EARTH_R = 6371000.0;

        double latStart = start.lat.ToRad();
        double latEnd = end.lat.ToRad();
        double dLat = (end.lat - start.lat).ToRad();
        double dLon = (end.lon - start.lon).ToRad();

        double s = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                   Math.Cos(latStart) * Math.Cos(latEnd) *
                   Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

        double c = 2 * Math.Atan2(Math.Sqrt(s), Math.Sqrt(1 - s));

        return c * EARTH_R;
    }

    public static List<(double x, double y)> ToMercator(List<(double lon, double lat)> pts)
    => pts.Select(p =>
    {
        var m = SphericalMercator.FromLonLat(p.lon, p.lat);
        return (m.x, m.y);
    }).ToList();

    public static (double t, double dPerp) ProjectPointToSegment(
        double x, double y,
        double startx, double starty,
        double endx, double endy)
    {
        double vx = endx - startx;
        double vy = endy - starty;
        double wx = x - startx;
        double wy = y - starty;

        double vv = Math.Pow(vx, 2) + Math.Pow(vy, 2);
        double t = vv <= 1e-9 ? 0 : (wx * vx + wy * vy) / vv;
        t = Math.Clamp(t, 0, 1);

        double px = startx + t * vx;
        double py = starty + t * vy;

        double dx = x - px;
        double dy = y - py;
        double d = Math.Sqrt(Math.Pow(dx, 2) + Math.Pow(dy, 2));

        return (t, d);
    }

    public static double AngleDiffDeg(double a, double b)
    {
        var d = (a - b) % 360.0;
        if (d < -180)
        { d += 360; }
        if (d > 180)
        { d -= 360; }

        d = Math.Abs(d);
        return d;
    }

    public static double BearingDegrees((double lon, double lat) start, (double lon, double lat) end)
    {
        double lat1 = start.lat.ToRad();
        double lat2 = end.lat.ToRad();
        double dLon = (end.lon - start.lon).ToRad();
        double y = Math.Sin(dLon) * Math.Cos(lat2);
        double x = Math.Cos(lat1) * Math.Sin(lat2) -
                   Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);

        double theta = Math.Atan2(y, x);
        double bearing = (theta.ToDeg() + 360) % 360;

        return bearing;
    }

#if ANDROID31_0_OR_GREATER
    public static Location ToMauiLocation(Android.Locations.Location aLoc)
    {
        var m = new Location(aLoc.Latitude, aLoc.Longitude)
        {
            Altitude = aLoc.HasAltitude ? aLoc.Altitude : null,
            Accuracy = aLoc.HasAccuracy ? aLoc.Accuracy : null,
            Speed = aLoc.HasSpeed ? aLoc.Speed : null,          
            Course = aLoc.HasBearing ? aLoc.Bearing : null,    
            Timestamp = DateTimeOffset.FromUnixTimeMilliseconds(aLoc.Time)
        };

        return m;
    }
#endif
}
