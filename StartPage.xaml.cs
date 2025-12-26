using Shiny.BluetoothLE;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace NBNavApp
{
    public partial class StartPage : ContentPage
    {
        readonly IBleManager ble;
        IDisposable? scanSub;

        IPeripheral? connectedDevice;
        BleCharacteristicInfo? navChar;

        const string SERVICE_UUID = "6b7b3c93-1fdc-4f5b-97be-14adb4ffbf4d";
        const string NAV_UUID = "6b7b3c94-1fdc-4f5b-97be-14adb4ffbf4d";

        public ObservableCollection<DeviceData> FoundDevices { get; } = new();

        public StartPage(IBleManager bleManager)
        {
            InitializeComponent();
            ble = bleManager;
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

        private async void OnDisconnectClicked(object sender, EventArgs e)
        {
            connectedDevice?.CancelConnection();
            connectedDevice = null;

            ConnDvc.Text = "Disconnected";
            ScanBtn.IsEnabled = true;
            DisconnectBtn.IsEnabled = false;
            NextPageBtn.IsVisible = false;
        }

        private async void OnNextPageClicked(object sender, EventArgs e)
        {
            await Shell.Current.GoToAsync("RoutePage");
        }

        private async void OnDeviceSelected(object sender, SelectionChangedEventArgs e)
        {
            DeviceData? selection = (DeviceData?)e.CurrentSelection.FirstOrDefault();
            if (selection == null)
            { return; }

            ScanBtn.IsEnabled = true;
            await ConnectAndCacheCharacteristic(selection.Peripheral);
        }

        private async Task ConnectAndCacheCharacteristic(IPeripheral peripheral)
        {
            scanSub?.Dispose();
            scanSub = null;

            connectedDevice?.CancelConnection();
            connectedDevice = peripheral;

            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(3));
            await connectedDevice.ConnectAsync(null, cts.Token);

            navChar = await connectedDevice.GetCharacteristicAsync(SERVICE_UUID, NAV_UUID, cts.Token);

            byte[] payload = BuildNavPacket(1, 1, 100, 0);
            await connectedDevice.WriteCharacteristicAsync(navChar, payload, false, cts.Token);

            FoundDevices.Clear();

            ConnDvc.Text = $"Connected to {connectedDevice.Name}";
            DisconnectBtn.IsEnabled = true;
            NextPageBtn.IsVisible = true;
            Devices.SelectedItem = null;
        }

        static byte[] BuildNavPacket(ushort seq, byte type, ushort distMeters, byte flags)
        {
            byte[] data =
            [
                (byte)(seq & 0xFF),
                (byte)((seq >> 8) & 0xFF),
                type,
                (byte)(distMeters & 0xFF),
                (byte)((distMeters >> 8) & 0xFF),
                flags,
            ];
            return data;
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
}
