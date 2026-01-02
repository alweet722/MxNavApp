using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using NetTopologySuite.Geometries;
using System.Diagnostics;

namespace NBNavApp;

public partial class RoutePage : ContentPage
{
    enum PointType
    {
        Start,
        Destination
    }

    readonly NavigationManager nav;
    readonly BleSender bleSender;
    readonly OffRouteDetector offRoute = new();
    readonly NavState navState = new();

    readonly Mapsui.Map map;

    MemoryLayer? pinLayer;
    MemoryLayer? routeLayer;
    MyLocationLayer? myLocation;

    List<(double lon, double lat)>? route;
    List<(double x, double y)>? routeXY;
    List<PreparedStep>? preparedSteps;
    List<string> avoidFeatures = new();

    double[]? totalDist;
    double[]? segLen;

    const string API_KEY = "eyJvcmciOiI1YjNjZTM1OTc4NTExMTAwMDFjZjYyNDgiLCJpZCI6ImE2Y2NjNGFmZjdhYTQ3NjliMjZjMTRjNmFmYjBjNDhlIiwiaCI6Im11cm11cjY0In0=";

    public RoutePage(NavigationManager nav, BleSender bleSender)
    {
        InitializeComponent();

        map = MapControl.Map;

        map.Layers.Add(OpenStreetMap.CreateTileLayer());

        (double x, double y) defaultCenter = SphericalMercator.FromLonLat(13.723076680216279, 51.05120761645636);
        map.Navigator.CenterOnAndZoomTo(defaultCenter.ToMPoint(), 10);

        this.nav = nav;
        this.bleSender = bleSender;

#if ANDROID31_0_OR_GREATER
        LocationBus.LocationUpdated += OnLocationUpdated;
#endif
        OffRouteDetector.GoneOffRoute += OnGoneOffRoute;
    }

    private void OnGoneOffRoute(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            navState.RouteState = RouteState.OFF_ROUTE;
            byte[] payload = BleSender.BuildNavPacket(0, 0, 0, (byte)navState.RouteState);
            await bleSender.WriteCharacteristicAsync(payload);
        });
    }

#if ANDROID31_0_OR_GREATER
    private void OnLocationUpdated(Android.Locations.Location location)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            // Debug.WriteLine($"{location.Latitude:F6} {location.Longitude:F6} ±{location.Accuracy}m");
            byte[] payload;
            CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

            if (myLocation == null)
            { return; }
            myLocation.IsMoving = true;
            var currentLocation = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);
            myLocation.UpdateMyLocation(currentLocation.ToMPoint(), true);

            var (s, dPerp) = RouteNavigation.MatchRouteToNextStep((currentLocation.x, currentLocation.y), routeXY, totalDist, segLen, preparedSteps, navState);

            bool reroute = offRoute.Update(dPerp, location.Accuracy, DateTime.UtcNow, out string reason);

            switch (navState.RouteState)
            {
                case RouteState.NORMAL:
                    break;
                case RouteState.OFF_ROUTE:
                    if (reroute)
                    { navState.RouteState = RouteState.REROUTE; }
                    return;
                case RouteState.REROUTE:
                    navState.Start = (location.Latitude, location.Longitude);
                    Debug.WriteLine(reason);
                    payload = BleSender.BuildNavPacket(0, 0, 0, (byte)RouteState.REROUTE);
                    await bleSender.WriteCharacteristicAsync(payload);

                    try
                    {
                        await RerouteAsync(
                            API_KEY,
                            [navState.Start.lon, navState.Start.lat],
                            [navState.Destination.lon, navState.Destination.lat],
                            avoidFeatures.ToArray());
                    }
                    catch (TaskCanceledException)
                    {
                        nav.StopNavigation();
                        return;
                    }

                    navState.RouteState = RouteState.NORMAL;
                    break;
            }

            var (idx, nextIdx, distToNext) = RouteNavigation.ComputeStepAndDistance(s, preparedSteps, navState);
            var nextStep = preparedSteps[nextIdx].type;

            payload = BleSender.BuildNavPacket((ushort)nextIdx, (byte)nextStep, (ushort)Math.Round(distToNext), (byte)RouteState.NORMAL);
            await bleSender.WriteCharacteristicAsync(payload);
        });
    }
#endif

    private void ShowPointOnMap(PointType type, double lat, double lon)
    {
        Mapsui.Styles.Color color;
        (double x, double y) point = SphericalMercator.FromLonLat(lon, lat);

        if (type == PointType.Start)
        { color = Mapsui.Styles.Color.LimeGreen; }
        else
        { color = Mapsui.Styles.Color.Maroon; }

        PointFeature feature = new(point.ToMPoint());
        feature.Styles.Add(new SymbolStyle
        {
            SymbolScale = 1.0,
            Fill = new(color)
        });

        if (pinLayer == null)
        {
            pinLayer = new()
            {
                Name = "PinLayer",
                Features = new[] { feature }
            };
        }
        else
        {
            if (pinLayer.Features == null)
            { pinLayer.Features = new[] { feature }; }
            else
            { pinLayer.Features = pinLayer.Features.Append(feature); }
        }

        if (!map.Layers.Contains(pinLayer))
        { map.Layers.Add(pinLayer); }

        map.Navigator.CenterOnAndZoomTo(point.ToMPoint(), 1, duration: 1000, easing: Mapsui.Animations.Easing.SinInOut);
        if (navState.Start != (0, 0) && navState.Destination != (0, 0))
        { RouteBtn.IsEnabled = true; }
        MapControl.Refresh();
    }

    private void ShowRoute(List<(double lon, double lat)> routePoints)
    {
        List<(double x, double y)> mercPts = routePoints.Select(p => SphericalMercator.FromLonLat(p.lon, p.lat)).ToList();
        Coordinate[] pts = mercPts.Select(p => new Coordinate(p.x, p.y)).ToArray();

        LineString lineString = new(pts);

        GeometryFeature feature = new()
        {
            Geometry = lineString,
        };

        feature.Styles.Add(new VectorStyle
        {
            Line = new Pen
            {
                Width = 6,
                Color = Mapsui.Styles.Color.Cyan
            }
        });

        if (routeLayer == null)
        {
            routeLayer = new()
            {
                Name = "Route",
                Features = new[] { feature }
            };
            map.Layers.Add(routeLayer);
        }
        else
        { routeLayer.Features = new[] { feature }; }

        Envelope env = lineString.EnvelopeInternal;
        MRect box = new(env.MinX, env.MinY, env.MaxX, env.MaxY);
        map.Navigator.ZoomToBox(box, duration: 1000, easing: Mapsui.Animations.Easing.SinInOut);

        MapControl.Refresh();
    }

    private async Task RerouteAsync(
    string apiKey,
    double[] newStart,
    double[] dest,
    string[] avoid)
    {
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));
        var res = await RouteNavigation.GetRoutingResponseAsync(apiKey, newStart, dest, avoid, cts.Token);
        if (res == null)
        { return; }

        var route = RouteNavigation.GetRoute(res);
        if (route == null)
        { return; }

        var steps = RouteNavigation.GetRoutingSteps(res);
        (totalDist, segLen) = RouteNavigation.BuildTotalDist(route);
        preparedSteps = RouteNavigation.PrepareSteps(steps, totalDist);
        routeXY = RouteNavigation.ToMercator(route);

        navState.CurrentStepIndex = 0;
        navState.LastSegIndex = 0;

        ShowRoute(route);
    }

    private async void OnEntryCompleted(object sender, EventArgs e)
    {
        Entry entry = (Entry)sender;
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        var geocode = await AddressGeocoder.GeocodeAddress(API_KEY, entry.Text, cts.Token);
        if (geocode == null)
        {
            DriveBtn.IsEnabled = false;
            return;
        }

        if (entry.ClassId == "DestAddrInput")
        {
            navState.Destination = (geocode.Value.lat, geocode.Value.lon);
            DestAddrInput.Text = geocode.Value.label;
            ShowPointOnMap(PointType.Destination, geocode.Value.lat, geocode.Value.lon);
        }
        else if (entry.ClassId == "StartAddrInput")
        {
            navState.Start = (geocode.Value.lat, geocode.Value.lon);
            StartAddrInput.Text = geocode.Value.label;
            ShowPointOnMap(PointType.Start, geocode.Value.lat, geocode.Value.lon);
        }
    }

    private async void OnRouteClicked(object sender, EventArgs e)
    {
        RouteBtn.IsEnabled = false;
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));

        if (AvoidHighwaysSw.IsToggled)
        { avoidFeatures.Add(RouteNavigation.AvoidFeatures.highways.ToString()); }
        if (AvoidTollSw.IsToggled)
        { avoidFeatures.Add(RouteNavigation.AvoidFeatures.tollways.ToString()); }

        var routingResponse = await RouteNavigation.GetRoutingResponseAsync(
            API_KEY,
            [navState.Start.lon, navState.Start.lat],
            [navState.Destination.lon, navState.Destination.lat],
            avoidFeatures.ToArray(),
            cts.Token);

        RouteBtn.IsEnabled = true;

        if (routingResponse == null)
        {
            DriveBtn.IsEnabled = false;
            return;
        }

        route = RouteNavigation.GetRoute(routingResponse);
        if (route == null)
        {
            DriveBtn.IsEnabled = false;
            return;
        }

        ShowRoute(route);

        var steps = RouteNavigation.GetRoutingSteps(routingResponse);
        (totalDist, segLen) = RouteNavigation.BuildTotalDist(route);
        preparedSteps = RouteNavigation.PrepareSteps(steps, totalDist);
        routeXY = RouteNavigation.ToMercator(route);

        TimeSpan timeToDestination = TimeSpan.FromSeconds(RouteNavigation.GetTimeToDest(routingResponse));
        DistanceToDestLabel.Text = $"{RouteNavigation.GetDistance(routingResponse) / 1000:F1} km";
        TimeToDestLabel.Text = $"{timeToDestination.Hours}:{timeToDestination.Minutes}";
        DriveBtn.IsEnabled = true;
    }

    private async void OnDriveClicked(object sender, EventArgs e)
    {
        myLocation ??= new(map, SphericalMercator.FromLonLat(navState.Start.lon, navState.Start.lat).ToMPoint());
        map.Layers.Add(myLocation);
        map.Navigator.CenterOnAndZoomTo(myLocation.MyLocation, 1, 1000, Mapsui.Animations.Easing.SinInOut);

        MapControl.Refresh();

        try
        { await nav.StartNavigationAsync(); }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Navigation", ex.Message, "Close");
            return;
        }

        StopBtn.IsEnabled = true;
        DriveBtn.IsEnabled = false;
        RouteBtn.IsEnabled = false;
    }

    private async void OnStopClicked(object sender, EventArgs e)
    {
        if (route != null)
        { ShowRoute(route); }

        if (myLocation != null)
        { map.Layers.Remove(myLocation); }

        nav.StopNavigation();

        await bleSender.WriteCharacteristicAsync(new byte[] { 0x00 });
        StopBtn.IsEnabled = false;
        DriveBtn.IsEnabled = true;
    }

    protected override bool OnBackButtonPressed()
    {
        if (!nav.IsNavigating)
        { return base.OnBackButtonPressed(); }

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            bool stop = await DisplayAlertAsync("Navigation", "Stop navigation?", "Yes", "No");
            if (!stop)
            { return; }

            nav.StopNavigation();
            StopBtn.IsEnabled = false;
            DriveBtn.IsEnabled = true;
            await Shell.Current.GoToAsync("..");
        });

        return true;
    }
}