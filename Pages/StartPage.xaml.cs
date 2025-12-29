using Shiny.BluetoothLE;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace NBNavApp;

public partial class StartPage : ContentPage
{
    readonly IBleManager ble;
    readonly BleConnectionState state;
    IDisposable? scanSub;

    IPeripheral? connectedDevice;
    BleCharacteristicInfo? navChar;
    DeviceData? selection;

    const string SERVICE_UUID = "6b7b3c93-1fdc-4f5b-97be-14adb4ffbf4d";
    const string NAV_UUID = "6b7b3c94-1fdc-4f5b-97be-14adb4ffbf4d";

    public ObservableCollection<DeviceData> FoundDevices { get; } = new();

    public StartPage(IBleManager bleManager, BleConnectionState connectionState)
    {
        InitializeComponent();
        ble = bleManager;
        state = connectionState;
        Devices.ItemsSource = FoundDevices;
    }

    private async void OnScanClicked(object sender, EventArgs e)
    {
        ScanBtn.IsEnabled = false;
        Shiny.AccessState access = await ble.RequestAccessAsync();
        if (access != Shiny.AccessState.Available)
        {
            await DisplayAlertAsync("BLE", $"No access: {access}", "Close");
            ScanBtn.IsEnabled = true;
            return;
        }

        FoundDevices.Clear();

        scanSub?.Dispose();
        scanSub = ble.Scan().Subscribe(s =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                string name = s.Peripheral.Name ?? "NO_NAME";
                string id = s.Peripheral.Uuid;
                int rssi = s.Rssi;

                if (FoundDevices.Any(x => x.Id == id))
                { return; }

                FoundDevices.Add(new(s.Peripheral, rssi));
            });
        });

        await Task.Delay(TimeSpan.FromSeconds(10));
        scanSub?.Dispose();
        scanSub = null;

        ScanBtn.IsEnabled = true;
    }

    private async void OnConnectionToggleClicked(object sender, EventArgs e)
    {
        if (connectedDevice != null)
        {
            await Disconnect();

            ConnDvc.Text = "Disconnected";
            ScanBtn.IsEnabled = true;
            ConnectionToggleBtn.IsEnabled = false;
            ConnectionToggleBtn.Text = "Connect";
            NextPageBtn.IsVisible = false;
        }
        else
        {
            if (selection == null)
            {
                ConnectionToggleBtn.IsEnabled = false;
                return;
            }

            scanSub?.Dispose();
            scanSub = null;

            ScanBtn.IsEnabled = true;
            ConnectionToggleBtn.IsEnabled = false;

            await ConnectAndCacheCharacteristic(selection.Peripheral);
            ConnectionToggleBtn.Text = "Disconnect";
            ConnectionToggleBtn.IsEnabled = true;

            ConnDvc.Text = $"Connected to {connectedDevice?.Name}";
            NextPageBtn.IsVisible = true;
            Devices.SelectedItem = null;
        }
    }

    private async void OnNextPageClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("RoutePage");
    }

    private async void OnDeviceSelected(object sender, SelectionChangedEventArgs e)
    {
        selection = (DeviceData?)e.CurrentSelection.FirstOrDefault();
        ConnectionToggleBtn.IsEnabled = true;
    }

    private async Task ConnectAndCacheCharacteristic(IPeripheral peripheral)
    {
        scanSub?.Dispose();
        scanSub = null;

        connectedDevice?.CancelConnection();
        connectedDevice = peripheral;

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await connectedDevice.ConnectAsync(null, cts.Token);

        navChar = await connectedDevice.GetCharacteristicAsync(SERVICE_UUID, NAV_UUID, cts.Token);

        byte[] payload = RouteNavigation.BuildNavPacket(1, 13, 100, 0);
        await connectedDevice.WriteCharacteristicAsync(navChar, payload, false, cts.Token);
        state.IsConnected = true;
    }

    private async Task Disconnect()
    {
        connectedDevice?.CancelConnection();
        connectedDevice = null;
        state.IsConnected = false;
    }
}

public class DeviceData
{
    public IPeripheral Peripheral { get; }
    public string Name => Peripheral.Name ?? "NO_NAME";
    public string Id => Peripheral.Uuid;
    public int Rssi { get; }
    public string Details => $"RSSI {Rssi} | {Id}";

    public DeviceData(IPeripheral peripheral, int rssi)
    {
        Peripheral = peripheral;
        Rssi = rssi;
    }
}