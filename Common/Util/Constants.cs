namespace NBNavApp.Common.Util;

public static class Constants
{
    public const string SERVICE_UUID = "6b7b3c93-1fdc-4f5b-97be-14adb4ffbf4d";
    public const string NAV_UUID = "6b7b3c94-1fdc-4f5b-97be-14adb4ffbf4d";
    public const string MX_NAV_NAME_KEY = "mx_nav_name";
    public const string MX_NAV_COLOR_KEY = "mx_nav_color";
    public const string API_KEY = "eyJvcmciOiI1YjNjZTM1OTc4NTExMTAwMDFjZjYyNDgiLCJpZCI6ImE2Y2NjNGFmZjdhYTQ3NjliMjZjMTRjNmFmYjBjNDhlIiwiaCI6Im11cm11cjY0In0=";

    public const double MAX_SPEED_KMH = 130;

    // Speed-dependent thresholds (adaptive)
    public const double OFFROUTE_MIN_DISTANCE_CITY = 30;        // 30m at 0 km/h
    public const double OFFROUTE_MIN_DISTANCE_HIGHWAY = 150;    // 150m at 130 km/h
    public const double NEXT_SEGMENT_MIN_DISTANCE_CITY = 5;     // 5m at 0 km/h
    public const double WRONGWAY_MIN_DISTANCE_HIGHWAY = 20;     // 20m at 130 km/h

    // Speed-dependent lookahead (adaptive)
    public const double LOOKAHEAD_MIN_CITY = 15;      // 15m at 0 km/h
    public const double LOOKAHEAD_MAX_HIGHWAY = 50;   // 50m at 130 km/h
}
