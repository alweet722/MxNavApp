using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling;
using Mapsui.UI.Maui;
using NetTopologySuite.Geometries;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;

namespace NBNavApp.ViewModels;

public class RoutePageViewModel : INotifyPropertyChanged
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

    MemoryLayer? pinLayer;
    MemoryLayer? routeLayer;
    MyLocationLayer? myLocation;

    List<(double lon, double lat)>? route;
    List<(double x, double y)>? routeXY;
    List<PreparedStep>? preparedSteps;
    List<string> avoidFeatures = new();

    double[]? totalDist;
    double[]? segLen;

    bool isRouting;
    public bool IsRouting
    {
        get => isRouting;
        set
        {
            if (isRouting == value) return;
            isRouting = value;
            OnPropertyChanged(nameof(IsRouting));
            ((Command)RouteCommand).ChangeCanExecute();
        }
    }

    bool isDriving;
    public bool IsDriving
    {
        get => isDriving;
        set
        {
            if (isDriving == value) return;
            isDriving = value;
            OnPropertyChanged(nameof(IsDriving));
            ((Command)DriveCommand).ChangeCanExecute();
        }
    }

    string? startAddressText;
    public string? StartAddressText
    {
        get => startAddressText;
        set
        {
            if (startAddressText == value) return;
            startAddressText = value;
            OnPropertyChanged(nameof(StartAddressText));
        }
    }

    string? destAddressText;
    public string? DestAddressText
    {
        get => destAddressText;
        set
        {
            if (destAddressText == value) return;
            destAddressText = value;
            OnPropertyChanged(nameof(DestAddressText));
        }
    }

    string? timeToDestText;
    public string? TimeToDestText
    {
        get => timeToDestText;
        set
        {
            if (timeToDestText == value) return;
            timeToDestText = value;
            OnPropertyChanged(nameof(TimeToDestText));
        }
    }

    string? distToDestText;
    public string? DistToDestText
    {
        get => distToDestText;
        set
        {
            if (distToDestText == value) return;
            distToDestText = value;
            OnPropertyChanged(nameof(DistToDestText));
        }
    }

    bool avoidHighways;
    public bool AvoidHighways
    {
        get => avoidHighways;
        set
        {
            if (avoidHighways == value) return;
            avoidHighways = value;
            OnPropertyChanged(nameof(AvoidHighways));
        }
    }

    bool avoidToll;
    public bool AvoidToll
    {
        get => avoidToll;
        set
        {
            if (avoidToll == value) return;
            avoidToll = value;
            OnPropertyChanged(nameof(AvoidToll));
        }
    }

    (double lat, double lon) startLocation;
    public (double lat, double lon) StartLocation
    {
        get => startLocation;
        set
        {
            if (startLocation == value) return;
            startLocation = value;
            OnPropertyChanged(nameof(StartLocation));
            ((Command)RouteCommand).ChangeCanExecute();
        }
    }

    (double lat, double lon) destLocation;
    public (double lat, double lon) DestLocation
    {
        get => destLocation;
        set
        {
            if (destLocation == value) return;
            destLocation = value;
            OnPropertyChanged(nameof(DestLocation));
            ((Command)RouteCommand).ChangeCanExecute();
        }
    }

    public Mapsui.Map Map { get; } = new()
    {
        CRS = "EPSG:3857"
    };

    public ICommand StartAddressCompletedCommand { get; }
    public ICommand DestAddressCompletedCommand { get; }
    public ICommand RouteCommand { get; }
    public ICommand DriveCommand { get; }
    public ICommand StopCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new(n));

    public RoutePageViewModel(NavigationManager nav, BleSender bleSender)
    {
        this.nav = nav;
        this.bleSender = bleSender;

        StartAddressCompletedCommand = new Command(async () => GeocodeAddressAsync(StartAddressText, PointType.Start));

        DestAddressCompletedCommand = new Command(async () => GeocodeAddressAsync(DestAddressText, PointType.Destination));

        RouteCommand = new Command(
            execute: async () => await RouteAsync(),
            canExecute: () => StartLocation != (0, 0) && DestLocation != (0, 0) && !IsRouting
            );

        DriveCommand = new Command(
            execute: async () => await StartDriveAsync(),
            canExecute: () => route != null && !IsDriving
            );

        StopCommand = new Command(
            execute: async () => await StopDriveAsync(),
            canExecute: () => IsDriving
            );

        Map.Layers.Add(OpenStreetMap.CreateTileLayer());

        (double x, double y) defaultCenter = SphericalMercator.FromLonLat(13.723076680216279, 51.05120761645636);
        Map.Navigator.CenterOnAndZoomTo(defaultCenter.ToMPoint(), 10);

        this.nav = nav;
        this.bleSender = bleSender;

#if ANDROID31_0_OR_GREATER
        LocationBus.LocationUpdated += OnLocationUpdated;
#endif
        OffRouteDetector.GoneOffRoute += OnGoneOffRoute;
    }

    public void NotifyUi()
    {
        OnPropertyChanged(nameof(IsRouting));
        OnPropertyChanged(nameof(IsDriving));

        ((Command)DriveCommand).ChangeCanExecute();
        ((Command)StopCommand).ChangeCanExecute();
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

    private async void GeocodeAddressAsync(string? address, PointType pointType)
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        if (string.IsNullOrEmpty(address))
        { return; }

        var geocode = await AddressGeocoder.GeocodeAddress(Constants.API_KEY, address, cts.Token);
        if (geocode == null)
        { return; }

        switch (pointType)
        {
            case PointType.Destination:
                DestLocation = navState.Destination = (geocode.Value.lat, geocode.Value.lon);
                DestAddressText = geocode.Value.label;
                ShowPointOnMap(PointType.Destination, geocode.Value.lat, geocode.Value.lon);
                break;
            case PointType.Start:
                StartLocation = navState.Start = (geocode.Value.lat, geocode.Value.lon);
                StartAddressText = geocode.Value.label;
                ShowPointOnMap(PointType.Start, geocode.Value.lat, geocode.Value.lon);
                break;
        };
    }

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

        if (!Map.Layers.Contains(pinLayer))
        { Map.Layers.Add(pinLayer); }

        Map.Navigator.CenterOnAndZoomTo(point.ToMPoint(), 1, duration: 1000, easing: Mapsui.Animations.Easing.SinInOut);

        Map.Refresh();
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
            Map.Layers.Add(routeLayer);
        }
        else
        { routeLayer.Features = new[] { feature }; }

        Envelope env = lineString.EnvelopeInternal;
        MRect box = new(env.MinX, env.MinY, env.MaxX, env.MaxY);
        Map.Navigator.ZoomToBox(box, duration: 1000, easing: Mapsui.Animations.Easing.SinInOut);

        Map.Refresh();
    }

    private async Task RouteAsync()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));

        if (AvoidHighways)
        { avoidFeatures.Add(RouteNavigation.AvoidFeatures.highways.ToString()); }
        if (AvoidToll)
        { avoidFeatures.Add(RouteNavigation.AvoidFeatures.tollways.ToString()); }

        IsRouting = true;
        NotifyUi();

        var routingResponse = await RouteNavigation.GetRoutingResponseAsync(
            Constants.API_KEY,
            [navState.Start.lon, navState.Start.lat],
            [navState.Destination.lon, navState.Destination.lat],
            avoidFeatures.ToArray(),
            cts.Token);

        if (routingResponse == null)
        {
            IsRouting = false;
            NotifyUi();

            return;
        }

        route = RouteNavigation.GetRoute(routingResponse);
        if (route == null)
        {
            IsRouting = false;
            NotifyUi();

            return;
        }

        ShowRoute(route);

        var steps = RouteNavigation.GetRoutingSteps(routingResponse);
        (totalDist, segLen) = RouteNavigation.BuildTotalDist(route);
        preparedSteps = RouteNavigation.PrepareSteps(steps, totalDist);
        routeXY = RouteNavigation.ToMercator(route);

        TimeSpan timeToDestination = TimeSpan.FromSeconds(RouteNavigation.GetTimeToDest(routingResponse));
        DistToDestText = $"{RouteNavigation.GetDistance(routingResponse) / 1000:F1} km";
        TimeToDestText = $"{timeToDestination.Hours}:{timeToDestination.Minutes}";

        IsRouting = false;
        NotifyUi();
    }

    private async Task StartDriveAsync()
    {
        myLocation ??= new(Map, SphericalMercator.FromLonLat(navState.Start.lon, navState.Start.lat).ToMPoint());
        Map.Layers.Add(myLocation);
        Map.Navigator.CenterOnAndZoomTo(myLocation.MyLocation, 1, 1000, Mapsui.Animations.Easing.SinInOut);
        Map.Refresh();

        try
        { await nav.StartNavigationAsync(); }
        catch (Exception ex)
        {
            await MauiAlertService.ShowAlertAsync("Navigation", ex.Message);
            return;
        }

        IsDriving = true;
        NotifyUi();
    }

    private async Task StopDriveAsync()
    {
        if (route != null)
        { ShowRoute(route); }

        if (myLocation != null)
        { Map.Layers.Remove(myLocation); }

        nav.StopNavigation();

        await bleSender.WriteCharacteristicAsync(new byte[] { 0x00 });

        IsDriving = false;
        NotifyUi();
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
                    // Debug.WriteLine(reason);
                    payload = BleSender.BuildNavPacket(0, 0, 0, (byte)RouteState.REROUTE);
                    await bleSender.WriteCharacteristicAsync(payload);

                    try
                    {
                        await RerouteAsync(
                            Constants.API_KEY,
                            [navState.Start.lon, navState.Start.lat],
                            [navState.Destination.lon, navState.Destination.lat],
                            avoidFeatures.ToArray());
                    }
                    catch (TaskCanceledException)
                    {
                        nav.StopNavigation();

                        IsDriving = false;
                        NotifyUi();
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
}
