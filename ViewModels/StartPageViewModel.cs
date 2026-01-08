using Shiny.BluetoothLE;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;

namespace NBNavApp.ViewModels;

public class StartPageViewModel : INotifyPropertyChanged
{
    readonly BleSender bleSender;

    public ObservableCollection<DeviceData> FoundDevices { get; } = new();

    DeviceData? myDevice;
    public DeviceData? MyDevice
    {
        get => myDevice;
        set
        {
            myDevice = value;
            OnPropertyChanged(nameof(MyDevice));
            OnPropertyChanged(nameof(MyDeviceIsEnabled));
        }
    }

    DeviceData? selectedFoundDevice;
    public DeviceData? SelectedFoundDevice
    {
        get => selectedFoundDevice;
        set
        {
            if (selectedFoundDevice == value)
            { return; }
            selectedFoundDevice = value;
            OnPropertyChanged(nameof(SelectedFoundDevice));
            ((Command)ToggleConnectCommand).ChangeCanExecute();

            MyDeviceIsSelected = false;
        }
    }

    bool isScanning;
    public bool IsScanning
    {
        get => isScanning;
        set
        {
            if (isScanning == value) return;
            isScanning = value;
            OnPropertyChanged(nameof(IsScanning));
        }
    }

    bool myDeviceIsSelected;
    public bool MyDeviceIsSelected
    {
        get => myDeviceIsSelected;
        set
        {
            if (myDeviceIsSelected == value) return;
            myDeviceIsSelected = value;
            OnPropertyChanged(nameof(MyDeviceIsSelected));
            ((Command)ToggleConnectCommand).ChangeCanExecute();
        }
    }

    bool isConnecting;
    public bool IsConnecting
    {
        get => isConnecting;
        set
        {
            if (isConnecting == value) return;
            isConnecting = value;
            OnPropertyChanged(nameof(IsConnecting));
            ((Command)ToggleConnectCommand).ChangeCanExecute();
        }
    }

    public bool MyDeviceIsEnabled => MyDevice?.Peripheral != null && bleSender.ConnectedDevice == null;
    public string ConnectionStatusText => bleSender.ConnectedDevice?.Name ?? "Disconnected";
    public string ConnectButtonText => bleSender.ConnectedDevice != null ? "Disconnect" : "Connect";
    public bool CanGoNext => bleSender.ConnectedDevice != null;
    public DeviceData? SelectedForConnect => MyDeviceIsSelected ? MyDevice : SelectedFoundDevice;

    public ICommand AppearingCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand SelectMyDeviceCommand { get; }
    public ICommand ToggleConnectCommand { get; }
    public ICommand GoToRouteCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand CleanupCommand { get; }


    string fav;

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new(n));

    public StartPageViewModel(BleSender bleSender)
    {
        this.bleSender = bleSender;

        fav = Preferences.Default.Get(Constants.DISPL_DEV_KEY, string.Empty);
        if (Preferences.Default.ContainsKey(Constants.DISPL_DEV_KEY) && !string.IsNullOrEmpty(fav))
        {
            DeviceData? dev = new(0, "N/A", fav);
            MyDevice = dev;
        }

        AppearingCommand = new Command(async () => await OnAppearingAsync());

        ScanCommand = new Command(async () => await ScanAsync(5));

        SelectMyDeviceCommand = new Command(() =>
        {
            if (MyDevice?.Peripheral == null)
            { return; }

            SelectedFoundDevice = null;

            MyDeviceIsSelected = true;
        });

        ToggleConnectCommand = new Command(
            execute: async () => await ToggleConnectAsync(),
            canExecute: () => (SelectedForConnect?.Peripheral != null || bleSender.ConnectedDevice != null) && !IsConnecting
            );

        GoToRouteCommand = new Command(async () =>
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                await MauiAlertService.ShowAlertAsync("Network", "No internet connection! Internet connection required for routing.");
                return;
            }
            await Shell.Current.GoToAsync("RoutePage");
        });

        ClearSelectionCommand = new Command(() =>
        {
            SelectedFoundDevice = null;
            MyDeviceIsSelected = false;
        });

        CleanupCommand = new Command(async () =>
        {
            await bleSender.Disconnect();
        });
    }

    private async Task OnAppearingAsync()
    {
        await CheckAndRequestLocationPermission();
        await CheckAndRequestNotificationPermission();

        await InitialScanAsync();
        NotifyUi();
    }

    private void NotifyUi()
    {
        OnPropertyChanged(nameof(MyDevice));
        OnPropertyChanged(nameof(MyDeviceIsEnabled));
        OnPropertyChanged(nameof(ConnectionStatusText));
        OnPropertyChanged(nameof(ConnectButtonText));
        OnPropertyChanged(nameof(CanGoNext));

        ((Command)ToggleConnectCommand).ChangeCanExecute();
    }

    private async Task InitialScanAsync()
    {
        await ScanAsync(2);
    }

    private async Task ScanAsync(int timeout)
    {
        try
        {
            Shiny.AccessState access = await bleSender.BleManager.RequestAccessAsync();
            if (access != Shiny.AccessState.Available)
            {
                IsScanning = false;
                await MauiAlertService.ShowAlertAsync("BLE", $"No access: {access}.");
                return;
            }

            FoundDevices.Clear();

            await bleSender.ScanDevicesAsync(FoundDevices, timeout);

            if (!string.IsNullOrEmpty(fav))
            {
                DeviceData? found = FoundDevices.FirstOrDefault(d => string.Equals(d.Name, fav, StringComparison.OrdinalIgnoreCase));

                if (found != null)
                { MyDevice = found; }
                else
                { MyDevice = new(0, "N/A", fav); }

                NotifyUi();
            }
        }
        finally
        { IsScanning = false; }
    }

    private async Task ToggleConnectAsync()
    {
        if (bleSender.ConnectedDevice != null)
        {
            await bleSender.Disconnect();

            SelectedFoundDevice = null;
            MyDeviceIsSelected = false;

            NotifyUi();

            await ScanAsync(2);
            return;
        }

        if (SelectedForConnect?.Peripheral == null)
        { return; }

        IsConnecting = true;
        bool status = await bleSender.ConnectAndCacheCharacteristic(SelectedForConnect.Peripheral);

        if (!status)
        {
            IsConnecting = false;
            MyDevice = new(0, "N/A", fav);
            MyDeviceIsSelected = false;
            NotifyUi();
            return;
        }

        Preferences.Default.Set(Constants.DISPL_DEV_KEY, bleSender.ConnectedDevice?.Name);
        fav = bleSender.ConnectedDevice?.Name ?? fav;

        MyDeviceIsSelected = false;
        IsConnecting = false;
        SelectedFoundDevice = null;

        NotifyUi();
    }

    private static async Task<PermissionStatus> CheckAndRequestLocationPermission()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
        if (status == PermissionStatus.Granted)
        { return status; }
        await Permissions.RequestAsync<Permissions.LocationAlways>();
        return status;
    }

    private static async Task<PermissionStatus> CheckAndRequestNotificationPermission()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
        if (status == PermissionStatus.Granted)
        { return status; }
        await Permissions.RequestAsync<Permissions.PostNotifications>();
        return status;
    }
}
