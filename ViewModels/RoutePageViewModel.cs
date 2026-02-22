using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Projections;
using Mapsui.Tiling;
using NBNavApp.Common.Ble;
using NBNavApp.Common.Messages;
using NBNavApp.Common.Navigation;
using NBNavApp.Common.Services;
using NBNavApp.Common.Util;
using Shiny.BluetoothLE;
using System.ComponentModel;
using System.Windows.Input;

namespace NBNavApp.ViewModels;

public partial class RoutePageViewModel : INotifyPropertyChanged
{
    enum PointType
    {
        Start,
        Destination
    }

    readonly NavigationManager nav;
    readonly BleSender bleSender;
    readonly OffRouteDetector offRoute = new();
    readonly WrongWayDetector wrongWayDetector = new();
    readonly NavState navState = new();
    readonly MovingAverage movingAverage = new();
    readonly MapService mapService;
    readonly BleStateMonitor bleStateMonitor;

    MyLocationLayer? myLocation;

    List<(double lon, double lat)>? route;
    List<(double x, double y)>? routeXY;
    List<PreparedStep>? preparedSteps;
    List<string> avoidFeatures = new();

    double[]? totalDist;
    double[]? segLen;

    int locUpdateCounter = 0;
    bool isStopping;
    const double lookahead = 40.0;
    TimeSpan timeToDest;
    SpeedState speedState;

    bool isRouting;
    public bool IsRouting
    {
        get => isRouting;
        set
        {
            if (isRouting == value) return;
            isRouting = value;
            OnPropertyChanged(nameof(IsRouting));
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
        }
    }

    public Mapsui.Map Map { get; } = new()
    {
        CRS = "EPSG:3857"
    };

    public ICommand StartAddressCompletedCommand { get; }
    public ICommand DestAddressCompletedCommand { get; }
    public ICommand UseMyLocationCommand { get; }
    public ICommand RouteCommand { get; }
    public ICommand DriveCommand { get; }
    public ICommand StopCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new(n));

    public RoutePageViewModel(NavigationManager nav, BleSender bleSender, BleStateMonitor bleStateMonitor)
    {
        this.nav = nav;
        this.bleSender = bleSender;
        this.bleStateMonitor = bleStateMonitor;

        mapService = new(Map);

        StartAddressCompletedCommand = new Command(async () => await GeocodeAddressAsync(StartAddressText, PointType.Start));

        DestAddressCompletedCommand = new Command(async () => await GeocodeAddressAsync(DestAddressText, PointType.Destination));

        UseMyLocationCommand = new Command(async () => await GetCurrentLocationAsync());

        RouteCommand = new Command(
            execute: async () => await RouteAsync(),
            canExecute: () => StartLocation != (0, 0) && DestLocation != (0, 0) && !IsRouting && !IsDriving
            );

        DriveCommand = new Command(
            execute: async () => await StartDriveAsync(),
            canExecute: () => route != null && !IsDriving
            );

        StopCommand = new Command(
            execute: async () => await PauseDriveAsync(),
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
        OffRouteDetector.ReturnedOnRoute += OnReturnedOnRoute;

        bleStateMonitor.PeripheralStateChanged += OnPeripheralStateChanged;
    }

    private void OnPeripheralStateChanged(object sender, BleStateEventArgs e)
    {
        if (e.State != ConnectionState.Disconnected)
        { return; }
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await StopDriveAsync();
        });
    }

    public void NotifyUi()
    {
        OnPropertyChanged(nameof(IsRouting));
        OnPropertyChanged(nameof(IsDriving));

        ((Command)RouteCommand).ChangeCanExecute();
        ((Command)DriveCommand).ChangeCanExecute();
        ((Command)StopCommand).ChangeCanExecute();
    }

    public async Task ReturnToStartPage()
    {
        if (IsDriving)
        {
            if (!await MauiAlertService.ShowAlertAsync("Navigation", "Do you want to stop the navigation?", "Yes", "No"))
            { return; }
        }

        await StopDriveAsync(0);
        await Shell.Current.GoToAsync("..");
    }

    private void OnGoneOffRoute(object? sender, EventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            navState.RouteState = RouteState.OFF_ROUTE;
            StateMessage msg = new(navState.RouteState);
            await bleSender.WriteCharacteristicAsync(msg);
        });
    }

    private void OnReturnedOnRoute(object? sender, EventArgs e)
    {
        navState.RouteState = RouteState.NORMAL;
    }

    private void ShowPointOnMap(PointType type, double lat, double lon)
    {
        var mapType = type == PointType.Start ? MapService.MapPointType.Start : MapService.MapPointType.Destination;
        mapService.ShowPoint(mapType, lat, lon);
    }

    private void ShowRoute(List<(double lon, double lat)> routePoints)
    {
        mapService.ShowRoute(routePoints);
    }

    private async Task GetCurrentLocationAsync()
    {
        Map.Refresh();

        Microsoft.Maui.Devices.Sensors.Location? location;
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        try
        {
            location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest
            {
                DesiredAccuracy = GeolocationAccuracy.High,
                Timeout = TimeSpan.FromSeconds(10)
            },
               cts.Token);
        }
        catch (TaskCanceledException)
        {
            await MauiAlertService.ShowAlertAsync("Location", "Could not get current location.");
            return;
        }

        if (location == null)
        { return; }

        StartLocation = navState.Start = (location.Latitude, location.Longitude);
        ShowPointOnMap(PointType.Start, location.Latitude, location.Longitude);

        StartAddressText = $"{location.Latitude:F6}°, {location.Longitude:F6}°";
        NotifyUi();
    }

    private async Task GeocodeAddressAsync(string? address, PointType pointType)
    {
        Map.Refresh();

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(10));
        if (string.IsNullOrEmpty(address))
        { return; }

        var geocode = await AddressGeocoder.GeocodeAddress(address, cts.Token);
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
        }

        NotifyUi();
    }

    private async Task RouteAsync()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));

        avoidFeatures = new();

        if (AvoidHighways)
        { avoidFeatures.Add(RouteNavigation.AvoidFeatures.highways.ToString()); }
        if (AvoidToll)
        { avoidFeatures.Add(RouteNavigation.AvoidFeatures.tollways.ToString()); }

        IsRouting = true;
        NotifyUi();

        var routingResponse = await RouteNavigation.GetRoutingResponseAsync(
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
        routeXY = GeoFunctions.ToMercator(route);

        timeToDest = TimeSpan.FromSeconds(RouteNavigation.GetTimeToDest(routingResponse));
        DistToDestText = $"{RouteNavigation.GetDistance(routingResponse) / 1000:F1} km";
        TimeToDestText = $"{timeToDest.Hours}:{timeToDest.Minutes}";

        IsRouting = false;
        NotifyUi();
    }

    private async Task RerouteAsync(
    double[] newStart,
    double[] dest,
    string[] avoid)
    {
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));
        var res = await RouteNavigation.GetRoutingResponseAsync(newStart, dest, avoid, cts.Token);
        if (res == null)
        { return; }

        var route = RouteNavigation.GetRoute(res);
        if (route == null)
        { return; }

        var steps = RouteNavigation.GetRoutingSteps(res);
        (totalDist, segLen) = RouteNavigation.BuildTotalDist(route);
        preparedSteps = RouteNavigation.PrepareSteps(steps, totalDist);
        routeXY = GeoFunctions.ToMercator(route);

        navState.CurrentStepIndex = 0;
        navState.LastSegIndex = 0;

        ShowRoute(route);
    }

    private async Task StartDriveAsync()
    {
        if (totalDist == null)
        { return; }

        if (!bleSender.ConnectionState.IsConnected)
        {
            await MauiAlertService.ShowAlertAsync("BLE", "Connection lost.");
            return;
        }

        try
        { await Geolocation.GetLocationAsync(); }
        catch (FeatureNotEnabledException)
        {
            await MauiAlertService.ShowAlertAsync("Navigation", "Geolocation is switched off.");
            return;
        }

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

        EtaMessage etaMsg = new(timeToDest);
        await bleSender.WriteCharacteristicAsync(etaMsg);

        DistMessage distMsg = new((uint)totalDist[^1]);
        await bleSender.WriteCharacteristicAsync(distMsg);

        IsDriving = true;
        NotifyUi();
    }

    private async Task PauseDriveAsync()
    {
        nav.StopNavigation();
        IsDriving = false;

        NotifyUi();
        await bleSender.WriteCharacteristicAsync(new ResetMessage());
    }

    private async Task StopDriveAsync(int delay = 0)
    {
        nav.StopNavigation();
        IsDriving = false;
        mapService.ClearRoute();

        mapService.ClearPoints();

        myLocation?.Enabled = false;

        myLocation = null;
        route = null;

        StartAddressText = string.Empty;
        DestAddressText = string.Empty;
        TimeToDestText = string.Empty;
        DistToDestText = string.Empty;

        AvoidHighways = false;
        AvoidToll = false;

        StartLocation = (0, 0);
        DestLocation = (0, 0);

        NotifyUi();

        if (!bleSender.ConnectionState.IsConnected)
        { return; }

        await Task.Delay(TimeSpan.FromSeconds(delay));
        await bleSender.WriteCharacteristicAsync(new ResetMessage());
    }

#if ANDROID31_0_OR_GREATER
    private void OnLocationUpdated(Android.Locations.Location location)
    {
        if (route == null || routeXY == null || totalDist == null || segLen == null || preparedSteps == null)
        { return; }

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (!bleSender.ConnectionState.IsConnected)
            {
                await MauiAlertService.ShowAlertAsync("BLE", "Connection lost.");
                await StopDriveAsync();
                return;
            }

            bool wrongWay = false;
            CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));

            if (myLocation == null)
            { return; }
            myLocation.IsMoving = true;
            var currentCartesianLoc = SphericalMercator.FromLonLat(location.Longitude, location.Latitude);
            myLocation.UpdateMyLocation(currentCartesianLoc.ToMPoint(), true);

            var (s, dPerp) = RouteNavigation.MatchRouteToNextStep(
                (currentCartesianLoc.x, currentCartesianLoc.y),
                routeXY,
                totalDist,
                segLen,
                preparedSteps,
                navState);

            var (idx, nextIdx, distToNext, exit) = RouteNavigation.ComputeStepAndDistance(s, preparedSteps, navState);
            var nextStep = preparedSteps[nextIdx].type;

            var pNow = RouteNavigation.PointAtS(route, totalDist, segLen, s);
            var pFwd = RouteNavigation.PointAtS(route, totalDist, segLen, s + lookahead);

            double bearing = GeoFunctions.BearingDegrees(pNow, pFwd);
            double? heading = location.HasBearing ? location.Bearing : null;
            if (heading != null)
            {
                double diff = GeoFunctions.AngleDiffDeg(bearing, heading.Value);
                wrongWay = wrongWayDetector.Update(location.Speed, dPerp, diff, DateTime.UtcNow);
            }

            bool reroute = offRoute.Update(dPerp, location.Accuracy, DateTime.UtcNow, out string reason);

            switch (navState.RouteState)
            {
                case RouteState.NORMAL:
                    if (wrongWay)
                    {
                        NavMessage uturnMsg = new(Instruction.U_TURN, uint.MaxValue, (byte)exit);
                        await bleSender.WriteCharacteristicAsync(uturnMsg);
                        return;
                    }
                    break;
                case RouteState.OFF_ROUTE:
                    if (reroute)
                    { navState.RouteState = RouteState.REROUTE; }
                    return;
                case RouteState.REROUTE:
                    navState.Start = (location.Latitude, location.Longitude);

                    try
                    {
                        await RerouteAsync(
                            [navState.Start.lon, navState.Start.lat],
                            [navState.Destination.lon, navState.Destination.lat],
                            avoidFeatures.ToArray());
                        
                        (s, dPerp) = RouteNavigation.MatchRouteToNextStep(
                            (currentCartesianLoc.x, currentCartesianLoc.y),
                            routeXY,
                            totalDist,
                            segLen,
                            preparedSteps,
                            navState);
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
            StateMessage stateMsg = new(navState.RouteState);
            await bleSender.WriteCharacteristicAsync(stateMsg);

            double remaining = Math.Max(0, totalDist[^1] - s);
            double tol = Math.Max(10.0, location.Accuracy);

            DistMessage distMsg = new((uint)remaining);
            await bleSender.WriteCharacteristicAsync(distMsg);

            DistToDestText = $"{remaining / 1000:F1} km";

            if (isStopping)
            { return; }
            if (remaining <= tol)
            {
                isStopping = true;
                NavMessage endMsg = new(Instruction.END, 0, 0);
                await bleSender.WriteCharacteristicAsync(endMsg);

                await StopDriveAsync(5);
                isStopping = false;
                return;
            }

            NavMessage navMsg = new(nextStep, (uint)distToNext, (byte)exit);
            await bleSender.WriteCharacteristicAsync(navMsg);

            Microsoft.Maui.Devices.Sensors.Location mLoc = GeoFunctions.ToMauiLocation(location);

            var (speed, next) = RouteNavigation.ComputeSpeed(mLoc, speedState);
            speedState = next;
            if (speed < 0.5)
            { return; }

            var avgSpeed = movingAverage.Compute(speed);

            locUpdateCounter++;
            if (locUpdateCounter < 20)
            { return; }

            locUpdateCounter = 0;

            var eta = remaining / avgSpeed;

            timeToDest = TimeSpan.FromSeconds(eta);
            EtaMessage etaMsg = new(timeToDest);
            await bleSender.WriteCharacteristicAsync(etaMsg);

            TimeToDestText = $"{timeToDest.Hours}:{timeToDest.Minutes:D2}";
        });
    }
#endif
}
