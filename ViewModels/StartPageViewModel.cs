using NBNavApp.Common.Interfaces;
using NBNavApp.Common.Messages.ParameterMessages;
using Shiny.BluetoothLE;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;

namespace NBNavApp.ViewModels;

public class StartPageViewModel : INotifyPropertyChanged
{
    readonly BleSender bleSender;
    private readonly ISettingsDialogService settingsDialog;
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
    public ICommand OpenSettingsCommand { get; }

    string fav;

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new(n));

    public StartPageViewModel(BleSender bleSender, ISettingsDialogService settingsDialog)
    {
        this.bleSender = bleSender;
        this.settingsDialog = settingsDialog;

        fav = Preferences.Default.Get(Constants.MX_NAV_NAME_KEY, string.Empty);
        if (Preferences.Default.ContainsKey(Constants.MX_NAV_NAME_KEY) && !string.IsNullOrEmpty(fav))
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

        OpenSettingsCommand = new Command(
            execute: async () =>
        {
            var res = await settingsDialog.ShowSettingsDialogAsync(MxNavName, MxNavColor);
            if (res == null)
            { return; }

            MxNavName = res.MxNavName;
            MxNavColor = res.MxNavColor;

            if (!string.IsNullOrEmpty(MxNavName))
            {
                foreach (var msg in NameMessage.CreateNameMessages(MxNavName))
                { await bleSender.WriteCharacteristicAsync(msg); }
            }

            if (MxNavColor != null)
            {
                ColorMessage colorMessage = new(MxNavColor.Color);
                await bleSender.WriteCharacteristicAsync(colorMessage);
            }
        },
            canExecute: () => bleSender.ConnectedDevice != null
        );
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
        OnPropertyChanged(nameof(CanGoNext));

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

        Preferences.Default.Set(Constants.MX_NAV_NAME_KEY, bleSender.ConnectedDevice?.Name);
        fav = bleSender.ConnectedDevice?.Name ?? fav;

        MyDevice = new(0, bleSender.ConnectedDevice?.Uuid, fav, bleSender.ConnectedDevice);

        MyDeviceIsSelected = false;
        IsConnecting = false;
        SelectedFoundDevice = null;

        NotifyUi();
    }

    public async Task DisposeAsync()
    {
        await bleSender.Disconnect();
        SelectedFoundDevice = null;
        MyDeviceIsSelected = false;

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

    private static async Task<PermissionStatus> CheckAndRequestBtPermission()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.Bluetooth>();
        if (status == PermissionStatus.Granted)
        { return status; }
        await Permissions.RequestAsync<Permissions.Bluetooth>();
        return status;
    }
}
