using Shiny.BluetoothLE;
using System.Collections.ObjectModel;

namespace NBNavApp;

public class BleSender
{
    const string SERVICE_UUID = "6b7b3c93-1fdc-4f5b-97be-14adb4ffbf4d";
    const string NAV_UUID = "6b7b3c94-1fdc-4f5b-97be-14adb4ffbf4d";

    BleCharacteristicInfo? navChar;
    IDisposable? scanSub;

    public IPeripheral? ConnectedDevice { get; set; }
    public BleConnectionState ConnectionState { get; }

    public BleSender(BleConnectionState connState)
    { ConnectionState = connState; }

    public async Task<List<DeviceData>> ScanDevicesAsync(IBleManager bleManager, ObservableCollection<DeviceData>? table = null)
    {
        List<DeviceData> peripherals = new();
        scanSub?.Dispose();
        scanSub = bleManager.Scan().Subscribe(s =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                string name = s.Peripheral.Name ?? "NO_NAME";
                string id = s.Peripheral.Uuid;
                int rssi = s.Rssi;

                if (peripherals.Any(x => x.Id == id))
                { return; }

                peripherals.Add(new(s.Peripheral, rssi));
                table?.Add(new(s.Peripheral, rssi));
            });
        });

        await Task.Delay(TimeSpan.FromSeconds(10));
        scanSub?.Dispose();
        scanSub = null;

        return peripherals;
    }

    public async Task ConnectAndCacheCharacteristic(IPeripheral peripheral)
    {
        scanSub?.Dispose();
        scanSub = null;

        ConnectedDevice?.CancelConnection();
        ConnectedDevice = peripheral;

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(5));
        await ConnectedDevice.ConnectAsync(null, cts.Token);

        navChar = await ConnectedDevice.GetCharacteristicAsync(SERVICE_UUID, NAV_UUID, cts.Token);

        ConnectionState.IsConnected = true;
    }

    public async Task WriteCharacteristicAsync(byte[] payload)
    {
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(3));

        if (ConnectedDevice == null)
        { return; }

        if (navChar == null)
        { return; }

        await ConnectedDevice.WriteCharacteristicAsync(navChar, payload, false, cts.Token);
    }

    public static byte[] BuildNavPacket(ushort seq, byte type, ushort distMeters, byte flags)
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

    public async Task Disconnect()
    {
        ConnectedDevice?.CancelConnection();
        ConnectedDevice = null;
        ConnectionState.IsConnected = false;
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
