using Mapsui;
using Mapsui.Extensions;
using Mapsui.Projections;
using Mapsui.Tiling;
using NBNavApp.Common.Ble;
using NBNavApp.Common.Navigation;
using NBNavApp.Common.Services;
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

    readonly NavigationService navigationService;
    readonly MapService mapService;
    readonly BleStateMonitor bleStateMonitor;

    (double lat, double lon) startLocation;
    (double lat, double lon) destLocation;
    List<string> avoidFeatures = new();

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

    public RoutePageViewModel(NavigationService navigationService, BleStateMonitor bleStateMonitor, BleConnectionState bleConnectionState)
    {
        this.navigationService = navigationService;
        this.bleStateMonitor = bleStateMonitor;

        mapService = new(Map);

        StartAddressCompletedCommand = new Command(async () => await GeocodeAddressAsync(StartAddressText, PointType.Start));

        DestAddressCompletedCommand = new Command(async () => await GeocodeAddressAsync(DestAddressText, PointType.Destination));

        UseMyLocationCommand = new Command(async () => await GetCurrentLocationAsync());

        RouteCommand = new Command(
            execute: async () => await RouteAsync(),
            canExecute: () => StartLocation != (0, 0) && DestLocation != (0, 0) && !IsDriving && !IsRouting
            );

        DriveCommand = new Command(
            execute: async () => await StartDriveAsync(),
            canExecute: () => navigationService.HasRoute && !IsDriving
            );

        StopCommand = new Command(
            execute: async () => await PauseDriveAsync(),
            canExecute: () => IsDriving
            );

        Map.Layers.Add(OpenStreetMap.CreateTileLayer());

        (double x, double y) defaultCenter = SphericalMercator.FromLonLat(13.723076680216279, 51.05120761645636);
        Map.Navigator.CenterOnAndZoomTo(defaultCenter.ToMPoint(), 10);

        navigationService.Initialize(new NavigationManager(bleConnectionState));
        navigationService.LocationUpdated += OnLocationUpdated;
        navigationService.NavigationStarted += (s, e) => OnNavigationStarted();
        navigationService.NavigationStopped += (s, e) => OnNavigationStopped();
        navigationService.NavigationPaused += (s, e) => OnNavigationPaused();
        navigationService.RouteUpdated += OnRouteUpdated;

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

        StartLocation = (location.Latitude, location.Longitude);
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
                DestLocation = (geocode.Value.lat, geocode.Value.lon);
                DestAddressText = geocode.Value.label;
                ShowPointOnMap(PointType.Destination, geocode.Value.lat, geocode.Value.lon);
                break;
            case PointType.Start:
                StartLocation = (geocode.Value.lat, geocode.Value.lon);
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
            [startLocation.lon, startLocation.lat],
            [destLocation.lon, destLocation.lat],
            avoidFeatures.ToArray(),
            cts.Token);

        if (routingResponse == null)
        {
            IsRouting = false;
            NotifyUi();
            return;
        }

        var route = RouteNavigation.GetRoute(routingResponse);
        if (route == null)
        {
            IsRouting = false;
            NotifyUi();
            return;
        }

        ShowRoute(route);

        var steps = RouteNavigation.GetRoutingSteps(routingResponse);
        var (totalDist, segLen) = RouteNavigation.BuildTotalDist(route);
        var preparedSteps = RouteNavigation.PrepareSteps(steps, totalDist);
        var routeXY = GeoFunctions.ToMercator(route);

        var timeToDest = TimeSpan.FromSeconds(RouteNavigation.GetTimeToDest(routingResponse));
        DistToDestText = $"{RouteNavigation.GetDistance(routingResponse) / 1000:F1} km";
        TimeToDestText = $"{timeToDest.Hours}:{timeToDest.Minutes}";

        navigationService.SetRoute(route, routeXY, totalDist, segLen, preparedSteps, timeToDest, startLocation, destLocation, avoidFeatures);

        IsRouting = false;
        NotifyUi();
    }

    private async Task StartDriveAsync()
    {
        if (!navigationService.HasRoute)
        { return; }

        await navigationService.StartNavigationAsync();
    }

    private async Task PauseDriveAsync()
    {
        await navigationService.PauseNavigationAsync();
    }

    private async Task StopDriveAsync(int delay = 0)
    {
        await navigationService.StopNavigationAsync(delay);

        mapService.ClearRoute();
        mapService.ClearPoints();
        mapService.ClearCurrentLocation();

        StartAddressText = string.Empty;
        DestAddressText = string.Empty;
        TimeToDestText = string.Empty;
        DistToDestText = string.Empty;

        AvoidHighways = false;
        AvoidToll = false;

        StartLocation = (0, 0);
        DestLocation = (0, 0);

        NotifyUi();
    }

    private void OnLocationUpdated(object? sender, LocationUpdateEventArgs e)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            mapService.UpdateCurrentLocation(e.Latitude, e.Longitude, e.Bearing);
            DistToDestText = $"{e.Remaining / 1000:F1} km";

            // Only update ETA when it's provided (every 20 location updates)
            if (e.ETA.HasValue)
            {
                TimeToDestText = $"{e.ETA.Value.Hours}:{e.ETA.Value.Minutes:D2}";
            }
        });
    }

    private void OnNavigationStarted()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            var navState = navigationService.GetNavigationState();
            if (navState != null)
            {
                mapService.InitializeLocationLayer(navState.Start.lat, navState.Start.lon);
            }

            IsDriving = true;
            NotifyUi();
        });
    }

    private void OnNavigationPaused()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsDriving = false;
            NotifyUi();
        });
    }

    private void OnNavigationStopped()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            IsDriving = false;
            NotifyUi();
        });
    }

    private void OnRouteUpdated(object? sender, List<(double lon, double lat)> updatedRoute)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            ShowRoute(updatedRoute);
        });
    }
}
