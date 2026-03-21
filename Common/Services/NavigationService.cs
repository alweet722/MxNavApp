using NBNavApp.Common.Ble;
using NBNavApp.Common.Messages;
using NBNavApp.Common.Navigation;
using NBNavApp.Common.Util;

namespace NBNavApp.Common.Services;

public class LocationUpdateEventArgs : EventArgs
{
    public double Remaining { get; set; }
    public TimeSpan? ETA { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Bearing { get; set; }
}

public class NavigationService
{
    readonly BleSender bleSender;
    readonly OffRouteDetector offRouteDetector = new();
    readonly WrongWayDetector wrongWayDetector = new();

    NavigationManager? navigationManager;

    List<(double lon, double lat)>? route;
    List<(double x, double y)>? routeXY;
    List<PreparedStep>? preparedSteps;
    List<string>? avoidFeatures;

    double[]? totalDist;
    double[]? segLen;

    NavState? navState;
    TimeSpan timeToDest;
    SpeedState speedState;

    int locUpdateCounter = 0;
    bool isStopping;
    const double lookahead = 25;

    bool isNavigating;
    public bool IsNavigating
    {
        get => isNavigating;
        private set => isNavigating = value;
    }

    public bool HasRoute => route != null;

    public event EventHandler<LocationUpdateEventArgs>? LocationUpdated;
    public event EventHandler<EventArgs>? NavigationStarted;
    public event EventHandler<EventArgs>? NavigationStopped;
    public event EventHandler<EventArgs>? NavigationPaused;
    public event EventHandler<List<(double lon, double lat)>>? RouteUpdated;

    public NavigationService(BleSender bleSender)
    {
        this.bleSender = bleSender;
        OffRouteDetector.GoneOffRoute += OnGoneOffRoute;
        OffRouteDetector.ReturnedOnRoute += OnReturnedOnRoute;
        WrongWayDetector.GoingWrongWay += OnGoingWrongWay;
        WrongWayDetector.Turned += OnTurned;
    }

    public void Initialize(NavigationManager navigationManager)
    {
        this.navigationManager = navigationManager;
    }

    public void SetRoute(
        List<(double lon, double lat)> routePoints,
        List<(double x, double y)> routePointsXY,
        double[] totalDistances,
        double[] segmentLengths,
        List<PreparedStep> steps,
        TimeSpan eta,
        (double lon, double lat) start,
        (double lon, double lat) destination,
        List<string> avoidFeaturesUsed)
    {
        route = routePoints;
        routeXY = routePointsXY;
        totalDist = totalDistances;
        segLen = segmentLengths;
        preparedSteps = steps;
        timeToDest = eta;
        avoidFeatures = avoidFeaturesUsed;

        navState = new NavState
        {
            Start = start,
            Destination = destination,
            RouteState = RouteState.NORMAL
        };

        locUpdateCounter = 0;
        isStopping = false;
        speedState = default;
    }

    public async Task StartNavigationAsync()
    {
        if (totalDist == null || navigationManager == null)
        {
            await MauiAlertService.ShowAlertAsync("Navigation", "Navigation not properly initialized.");
            return;
        }

        if (!bleSender.ConnectionState.IsConnected)
        {
            await MauiAlertService.ShowAlertAsync("BLE", "Connection lost.");
            return;
        }

        try
        { await Geolocation.GetLocationAsync(); }
        catch (FeatureNotEnabledException)
        { await MauiAlertService.ShowAlertAsync("Navigation", "Geolocation is switched off."); return; }

        if (navState == null)
        {
            await MauiAlertService.ShowAlertAsync("Navigation", "Route not set.");
            return;
        }

        await navigationManager.StartNavigationAsync();

#if ANDROID31_0_OR_GREATER
        LocationBus.LocationUpdated += OnLocationUpdated;
#endif

        EtaMessage etaMsg = new(timeToDest);
        try
        { await bleSender.WriteCharacteristicAsync(etaMsg); }
        catch (BleWriteFailedException)
        {
            await StopNavigationAsync();
            return;
        }

        DistMessage distMsg = new((uint)totalDist[^1]);
        try
        { await bleSender.WriteCharacteristicAsync(distMsg); }
        catch (BleWriteFailedException)
        {
            await StopNavigationAsync();
            return;
        }

        IsNavigating = true;
        NavigationStarted?.Invoke(this, EventArgs.Empty);
    }

    public async Task PauseNavigationAsync()
    {
        if (navigationManager == null)
        { return; }

        navigationManager.StopNavigation();
        IsNavigating = false;

        try
        { await bleSender.WriteCharacteristicAsync(new ResetMessage()); }
        catch (BleWriteFailedException)
        { return; }
        NavigationPaused?.Invoke(this, EventArgs.Empty);
    }

    public async Task StopNavigationAsync(int delay = 0)
    {
        if (navigationManager == null)
        { return; }

        navigationManager.StopNavigation();
        IsNavigating = false;

        route = null;
        routeXY = null;
        totalDist = null;
        segLen = null;
        preparedSteps = null;
        navState = null;

#if ANDROID31_0_OR_GREATER
        LocationBus.LocationUpdated -= OnLocationUpdated;
#endif

        if (!bleSender.ConnectionState.IsConnected)
        {
            NavigationStopped?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (delay > 0)
        { await Task.Delay(TimeSpan.FromSeconds(delay)); }

        try
        { await bleSender.WriteCharacteristicAsync(new ResetMessage()); }
        catch (BleWriteFailedException)
        {
            NavigationStopped?.Invoke(this, EventArgs.Empty);
            return;
        }
        NavigationStopped?.Invoke(this, EventArgs.Empty);
    }

#if ANDROID31_0_OR_GREATER
    private async void OnLocationUpdated(Android.Locations.Location location)
    {
        if (route == null || routeXY == null || totalDist == null || segLen == null || preparedSteps == null || navState == null)
        { return; }

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (!bleSender.ConnectionState.IsConnected)
            {
                await MauiAlertService.ShowAlertAsync("BLE", "Connection lost.");
                await StopNavigationAsync();
                return;
            }

            bool wrongWay = false;
            bool wrongWayReroute = false;
            bool reroute = false;
            DateTime now = DateTime.UtcNow;

            var currentCartesianLoc = Mapsui.Projections.SphericalMercator.FromLonLat(location.Longitude, location.Latitude);

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
                wrongWay = wrongWayDetector.Update(location.Speed, diff, now);
                wrongWayReroute = wrongWayDetector.UpdateReroute(now);
            }

            if (!wrongWay)
            { reroute = offRouteDetector.Update(dPerp, location.Accuracy, now); }

            StateMessage stateMsg = new(navState.RouteState);
            try
            { await bleSender.WriteCharacteristicAsync(stateMsg); }
            catch (BleWriteFailedException)
            {
                await StopNavigationAsync();
                return;
            }

            switch (navState.RouteState)
            {
                case RouteState.NORMAL:
                    break;
                case RouteState.WRONG_WAY:
                    if (wrongWayReroute)
                    { navState.RouteState = RouteState.REROUTE; }
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
                            avoidFeatures?.ToArray() ?? []);

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
                        await StopNavigationAsync();
                        return;
                    }

                    navState.RouteState = RouteState.NORMAL;
                    break;
            }

            double remaining = Math.Max(0, totalDist[^1] - s);
            double tol = Math.Max(10.0, location.Accuracy);

            DistMessage distMsg = new((uint)remaining);
            try
            { await bleSender.WriteCharacteristicAsync(distMsg); }
            catch (BleWriteFailedException)
            {
                await StopNavigationAsync();
                return;
            }

            if (isStopping)
            { return; }

            if (remaining <= tol)
            {
                isStopping = true;
                NavMessage endMsg = new(Instruction.END, 0, 0);
                try
                { await bleSender.WriteCharacteristicAsync(endMsg); }
                catch (BleWriteFailedException)
                {
                    await StopNavigationAsync();
                    isStopping = false;
                    return;
                }

                await StopNavigationAsync(5);
                isStopping = false;
                return;
            }

            int distToNextRounded = ((int)distToNext).RoundToTens();

            NavMessage navMsg = new(nextStep, (uint)distToNextRounded, (byte)exit);
            try
            { await bleSender.WriteCharacteristicAsync(navMsg); }
            catch (BleWriteFailedException)
            {
                await StopNavigationAsync();
                return;
            }

            Microsoft.Maui.Devices.Sensors.Location mLoc = GeoFunctions.ToMauiLocation(location);

            var (speed, next) = RouteNavigation.ComputeSpeed(mLoc, speedState);
            speedState = next;
            if (speed < 0.5)
            { return; }

            var avgSpeed = MovingAverage.Compute(speed);

            locUpdateCounter++;
            if (locUpdateCounter >= 20)
            {
                var eta = remaining / avgSpeed;
                timeToDest = TimeSpan.FromSeconds(eta);
                EtaMessage etaMsg = new(timeToDest);
                try
                { await bleSender.WriteCharacteristicAsync(etaMsg); }
                catch (BleWriteFailedException)
                {
                    await StopNavigationAsync();
                    return;
                }
                locUpdateCounter = 0;
            }

            LocationUpdated?.Invoke(this, new LocationUpdateEventArgs
            {
                Remaining = remaining,
                ETA = timeToDest,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                Bearing = heading
            });
        });
    }
#endif

    private async Task RerouteAsync(
        double[] newStart,
        double[] dest,
        string[] avoid)
    {
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));
        var res = await OpenRouteServiceFunctions.GetRoutingResponseAsync(newStart, dest, avoid, cts.Token);
        if (res == null) { return; }

        var newRoute = OpenRouteServiceFunctions.GetRoute(res);
        if (newRoute == null) { return; }

        var steps = OpenRouteServiceFunctions.GetRoutingSteps(res);
        (totalDist, segLen) = RouteNavigation.BuildTotalDist(newRoute);
        preparedSteps = RouteNavigation.PrepareSteps(steps, totalDist);
        routeXY = GeoFunctions.ToMercator(newRoute);
        route = newRoute;

        if (navState != null)
        {
            navState.CurrentStepIndex = 0;
            navState.LastSegIndex = 0;
        }

        RouteUpdated?.Invoke(this, route);
    }

    private void OnGoneOffRoute(object? sender, EventArgs e)
    {
        navState?.RouteState = RouteState.OFF_ROUTE;
    }

    private void OnReturnedOnRoute(object? sender, EventArgs e)
    {
        navState?.RouteState = RouteState.NORMAL;
    }

    private void OnGoingWrongWay(object? sender, EventArgs e)
    {
        navState?.RouteState = RouteState.WRONG_WAY;
    }

    private void OnTurned(object? sender, EventArgs e)
    {
        navState?.RouteState = RouteState.NORMAL;
    }


    public NavState? GetNavigationState() => navState;
}
