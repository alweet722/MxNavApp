using NBNavApp.Common.Ble;
using NBNavApp.Common.Messages;
using NBNavApp.Common.Navigation;
using NBNavApp.Common.Util;

namespace NBNavApp.Common.Services;

public class LocationUpdateEventArgs : EventArgs
{
    public double S { get; set; }
    public double DPerp { get; set; }
    public double Remaining { get; set; }
    public TimeSpan? ETA { get; set; }
    public int NextStepIndex { get; set; }
    public Instruction NextInstruction { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double? Bearing { get; set; }
}

public class NavigationService
{
    readonly BleSender bleSender;
    readonly OffRouteDetector offRoute = new();
    readonly WrongWayDetector wrongWayDetector = new();
    readonly MovingAverage movingAverage = new();

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
        {
            await Geolocation.GetLocationAsync();
        }
        catch (FeatureNotEnabledException)
        {
            await MauiAlertService.ShowAlertAsync("Navigation", "Geolocation is switched off.");
            return;
        }

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
        await bleSender.WriteCharacteristicAsync(etaMsg);

        DistMessage distMsg = new((uint)totalDist[^1]);
        await bleSender.WriteCharacteristicAsync(distMsg);

        IsNavigating = true;
        NavigationStarted?.Invoke(this, EventArgs.Empty);
    }

    public async Task PauseNavigationAsync()
    {
        if (navigationManager == null)
        {
            return;
        }

        navigationManager.StopNavigation();
        IsNavigating = false;

        await bleSender.WriteCharacteristicAsync(new ResetMessage());
        NavigationPaused?.Invoke(this, EventArgs.Empty);
    }

    public async Task StopNavigationAsync(int delay = 0)
    {
        if (navigationManager == null)
        {
            return;
        }

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
        {
            await Task.Delay(TimeSpan.FromSeconds(delay));
        }

        await bleSender.WriteCharacteristicAsync(new ResetMessage());
        NavigationStopped?.Invoke(this, EventArgs.Empty);
    }

#if ANDROID31_0_OR_GREATER
    private async void OnLocationUpdated(Android.Locations.Location location)
    {
        if (route == null || routeXY == null || totalDist == null || segLen == null || preparedSteps == null || navState == null)
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (!bleSender.ConnectionState.IsConnected)
            {
                await MauiAlertService.ShowAlertAsync("BLE", "Connection lost.");
                await StopNavigationAsync();
                return;
            }

            bool wrongWay = false;

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
                wrongWay = wrongWayDetector.Update(location.Speed, dPerp, diff, DateTime.UtcNow);
            }

            bool reroute = offRoute.Update(dPerp, location.Accuracy, DateTime.UtcNow, out string reason);

            // Raise location updated event for UI updates (marker position, bearing)
            // This is invoked early so the marker updates on every location
            double remaining = Math.Max(0, totalDist[^1] - s);
            LocationUpdated?.Invoke(this, new LocationUpdateEventArgs
            {
                S = s,
                DPerp = dPerp,
                Remaining = remaining,
                ETA = null,
                NextStepIndex = nextIdx,
                NextInstruction = nextStep,
                Latitude = location.Latitude,
                Longitude = location.Longitude,
                Bearing = heading
            });

            switch (navState.RouteState)
            {
                case RouteState.NORMAL:
                    StateMessage stateMsg = new(navState.RouteState);
                    await bleSender.WriteCharacteristicAsync(stateMsg);
                    if (wrongWay)
                    {
                        NavMessage uturnMsg = new(Instruction.U_TURN, uint.MaxValue, (byte)exit);
                        await bleSender.WriteCharacteristicAsync(uturnMsg);
                        return;
                    }
                    break;
                case RouteState.OFF_ROUTE:
                    stateMsg = new(navState.RouteState);
                    await bleSender.WriteCharacteristicAsync(stateMsg);
                    if (reroute)
                    {
                        navState.RouteState = RouteState.REROUTE;
                    }
                    return;
                case RouteState.REROUTE:
                    stateMsg = new(navState.RouteState);
                    await bleSender.WriteCharacteristicAsync(stateMsg);
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

            double tol = Math.Max(10.0, location.Accuracy);

            DistMessage distMsg = new((uint)remaining);
            await bleSender.WriteCharacteristicAsync(distMsg);

            if (isStopping)
            {
                return;
            }

            if (remaining <= tol)
            {
                isStopping = true;
                NavMessage endMsg = new(Instruction.END, 0, 0);
                await bleSender.WriteCharacteristicAsync(endMsg);

                await StopNavigationAsync(5);
                isStopping = false;
                return;
            }

            NavMessage navMsg = new(nextStep, (uint)distToNext, (byte)exit);
            await bleSender.WriteCharacteristicAsync(navMsg);

            Microsoft.Maui.Devices.Sensors.Location mLoc = GeoFunctions.ToMauiLocation(location);

            var (speed, next) = RouteNavigation.ComputeSpeed(mLoc, speedState);
            speedState = next;
            if (speed < 0.5)
            {
                return;
            }

            var avgSpeed = movingAverage.Compute(speed);

            locUpdateCounter++;
            if (locUpdateCounter < 20)
            {
                return;
            }

            locUpdateCounter = 0;

            var eta = remaining / avgSpeed;
            timeToDest = TimeSpan.FromSeconds(eta);
            EtaMessage etaMsg = new(timeToDest);
            await bleSender.WriteCharacteristicAsync(etaMsg);

            // Update location event with ETA info
            LocationUpdated?.Invoke(this, new LocationUpdateEventArgs
            {
                S = s,
                DPerp = dPerp,
                Remaining = remaining,
                ETA = timeToDest,
                NextStepIndex = nextIdx,
                NextInstruction = nextStep,
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
        var res = await RouteNavigation.GetRoutingResponseAsync(newStart, dest, avoid, cts.Token);
        if (res == null)
        {
            return;
        }

        var newRoute = RouteNavigation.GetRoute(res);
        if (newRoute == null)
        {
            return;
        }

        var steps = RouteNavigation.GetRoutingSteps(res);
        (totalDist, segLen) = RouteNavigation.BuildTotalDist(newRoute);
        preparedSteps = RouteNavigation.PrepareSteps(steps, totalDist);
        routeXY = GeoFunctions.ToMercator(newRoute);
        route = newRoute;

        if (navState != null)
        {
            navState.CurrentStepIndex = 0;
            navState.LastSegIndex = 0;
        }

        // Notify that the route has been updated
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

    public NavState? GetNavigationState() => navState;
    public bool HasRoute => route != null;
}
