namespace NBNavApp.Common.Navigation;

public static class LocationBus
{
#if ANDROID31_0_OR_GREATER
    public static event Action<Android.Locations.Location>? LocationUpdated;

    public static void Publish(Android.Locations.Location location)
    {
        LocationUpdated?.Invoke(location);
    }
#endif
}