using NBNavApp.Common.Ble;
using NBNavApp.Common.Util;
using Shiny.BluetoothLE;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

namespace NBNavApp.ViewModels;

public partial class StartPageViewModel : INotifyPropertyChanged
{
    readonly BleInterface bleInterface;

    public ObservableCollection<DeviceData> FoundDevices { get; } = new();

    public string? MxNavName { get; set; }
    public ColorEntry? MxNavColor { get; set; }

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

    bool settingsOpen;
    public bool SettingsOpen
    {
        get => settingsOpen;
        set
        {
            if (settingsOpen == value) return;
            settingsOpen = value;
            OnPropertyChanged(nameof(SettingsOpen));
        }
    }

    public bool MyDeviceIsEnabled => MyDevice?.Peripheral != null && bleInterface.ConnectedDevice == null;
    public string ConnectionStatusText => bleInterface.ConnectedDevice?.Name ?? "Disconnected";
    public string ConnectButtonText => bleInterface.ConnectedDevice != null ? "Disconnect" : "Connect";
    public bool CanRoute => bleInterface.ConnectedDevice != null;
    public DeviceData? SelectedForConnect => MyDeviceIsSelected ? MyDevice : SelectedFoundDevice;

    public ICommand AppearingCommand { get; }
    public ICommand ScanCommand { get; }
    public ICommand SelectMyDeviceCommand { get; }
    public ICommand ToggleConnectCommand { get; }
    public ICommand GoToRouteCommand { get; }
    public ICommand ClearSelectionCommand { get; }
    public ICommand OpenSettingsCommand { get; }

    string favDeviceName;

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new(n));

    public StartPageViewModel(BleInterface bleInterface)
    {
        this.bleInterface = bleInterface;

        favDeviceName = Preferences.Default.Get(Constants.MX_NAV_NAME_KEY, string.Empty);
        if (Preferences.Default.ContainsKey(Constants.MX_NAV_NAME_KEY) && !string.IsNullOrEmpty(favDeviceName))
        {
            DeviceData? dev = new(0, "N/A", favDeviceName);
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
            canExecute: () => (SelectedForConnect?.Peripheral != null || bleInterface.ConnectedDevice != null) && !IsConnecting
            );

        GoToRouteCommand = new Command(async () =>
        {
            if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
            {
                await MauiPopupService.ShowAlertAsync("Network", "No internet connection! Internet connection required for routing.");
                return;
            }
            string apiKey = Preferences.Default.Get(Constants.API_KEY_KEY, string.Empty);
            if (string.IsNullOrEmpty(apiKey))
            {
                await MauiPopupService.ShowAlertAsync("ORS API key", "ORS API key not set;\nRouting not possible.");
                return;
            }
            await Shell.Current.GoToAsync("RoutePage");
        });

        ClearSelectionCommand = new Command(() =>
        {
            SelectedFoundDevice = null;
            MyDeviceIsSelected = false;
        });

        OpenSettingsCommand = new Command(async () =>
        {
            await Shell.Current.GoToAsync("SettingsPage");
        });

        bleInterface.BleConnectionStateChanged += OnBleConnectionStateChanged;
    }

    private void OnBleConnectionStateChanged(object? sender, BleStateEventArgs e)
    {
        NotifyUi();
    }

    private async Task OnAppearingAsync()
    {
        await CheckAndRequestBtPermission();
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
        OnPropertyChanged(nameof(CanRoute));
        OnPropertyChanged(nameof(SettingsOpen));

        ((Command)ToggleConnectCommand).ChangeCanExecute();
        ((Command)OpenSettingsCommand).ChangeCanExecute();
    }

    private async Task InitialScanAsync()
    {
        await ScanAsync(2);
    }

    private async Task ScanAsync(int timeout)
    {
        try
        {
            Shiny.AccessState access = await bleInterface.BleManager.RequestAccessAsync();
            if (access != Shiny.AccessState.Available)
            {
                IsScanning = false;
                await MauiPopupService.ShowAlertAsync("BLE", $"No access: {access}.");
                return;
            }

            FoundDevices.Clear();

            await bleInterface.ScanDevicesAsync(FoundDevices, timeout);

            if (!string.IsNullOrEmpty(favDeviceName))
            {
                DeviceData? found = FoundDevices.FirstOrDefault(d => string.Equals(d.Name, favDeviceName, StringComparison.OrdinalIgnoreCase));

                if (found != null)
                { MyDevice = found; }
                else
                { MyDevice = new(0, "N/A", favDeviceName); }

                NotifyUi();
            }
        }
        finally
        { IsScanning = false; }
    }

    private async Task ToggleConnectAsync()
    {
        if (bleInterface.ConnectedDevice != null)
        {
            await bleInterface.Disconnect();

            SelectedFoundDevice = null;
            MyDeviceIsSelected = false;

            await ScanAsync(2);
            return;
        }

        if (SelectedForConnect?.Peripheral == null)
        { return; }

        IsConnecting = true;
        bool status = await bleInterface.ConnectAndCacheCharacteristic(SelectedForConnect.Peripheral);

        if (!status || bleInterface.ConnectedDevice == null)
        {
            IsConnecting = false;
            MyDevice = new(0, "N/A", favDeviceName);
            MyDeviceIsSelected = false;

            return;
        }

        Preferences.Default.Set(Constants.MX_NAV_NAME_KEY, bleInterface.ConnectedDevice?.Name);
        favDeviceName = bleInterface.ConnectedDevice?.Name ?? favDeviceName;

        MyDevice = new(0, bleInterface.ConnectedDevice.Uuid, favDeviceName, bleInterface.ConnectedDevice);

        MyDeviceIsSelected = false;
        IsConnecting = false;
        SelectedFoundDevice = null;
    }

    public async Task DisposeAsync()
    {
        await bleInterface.Disconnect();
        SelectedFoundDevice = null;
        MyDeviceIsSelected = false;

        NotifyUi();
    }

    private static async Task<PermissionStatus> CheckAndRequestLocationPermission()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
        if (status == PermissionStatus.Granted)
        { return status; }
        status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        return status;
    }

    private static async Task<PermissionStatus> CheckAndRequestNotificationPermission()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
        if (status == PermissionStatus.Granted)
        { return status; }
        status = await Permissions.RequestAsync<Permissions.PostNotifications>();
        return status;
    }

    private static async Task<PermissionStatus> CheckAndRequestBtPermission()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Bluetooth>();
        if (status == PermissionStatus.Granted)
        { return status; }
        status = await Permissions.RequestAsync<Permissions.Bluetooth>();
        return status;
    }
}
