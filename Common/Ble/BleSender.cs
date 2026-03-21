using NBNavApp.Common.Messages;
using Shiny.BluetoothLE;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace NBNavApp.Common.Ble;

public class BleSender
{
    BleCharacteristicInfo? navChar;
    IDisposable? scanSub;

    public IBleManager BleManager { get; set; }
    public IPeripheral? ConnectedDevice { get; set; }
    public BleConnectionState ConnectionState { get; }

    public BleSender(BleConnectionState connState, IBleManager bleManager)
    {
        ConnectionState = connState;
        BleManager = bleManager;
    }

    public async Task<List<DeviceData>> ScanDevicesAsync(ObservableCollection<DeviceData>? table = null, int timeout = 10)
    {
        List<DeviceData> peripherals = new();

        if (timeout < 0)
        { return peripherals; }

        if (BleManager.IsScanning)
        {
            await MauiAlertService.ShowAlertAsync("BLE", "Another scan already in progress.");
            return peripherals;
        }

        scanSub?.Dispose();
        scanSub = BleManager.Scan(new ScanConfig(Constants.SERVICE_UUID)).Subscribe(s =>
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                string name = s.Peripheral.Name ?? "NO_NAME";
                string id = s.Peripheral.Uuid;
                int rssi = s.Rssi;

                if (peripherals.Any(x => x.Id == id))
                { return; }

                peripherals.Add(new(rssi, id, name, s.Peripheral));
                table?.Add(new(rssi, id, name, s.Peripheral));
            });
        });

        await Task.Delay(TimeSpan.FromSeconds(timeout));
        scanSub?.Dispose();
        scanSub = null;

        return peripherals;
    }

    public async Task<bool> ConnectAndCacheCharacteristic(IPeripheral peripheral)
    {
        scanSub?.Dispose();
        scanSub = null;

        ConnectedDevice?.CancelConnection();
        ConnectedDevice = peripheral;

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(15));
        try
        { await ConnectedDevice.ConnectAsync(null, cts.Token); }
        catch (TaskCanceledException)
        {
            ConnectedDevice = null;
            await MauiAlertService.ShowAlertAsync("BLE", "Connection timed out.");
            return false;
        }

        try
        { navChar = await ConnectedDevice.GetCharacteristicAsync(Constants.SERVICE_UUID, Constants.NAV_UUID, cts.Token); }
        catch (TaskCanceledException)
        {
            ConnectedDevice.CancelConnection();
            ConnectedDevice = null;
            await MauiAlertService.ShowAlertAsync("BLE", "Timeout while getting navigation characteristic.");
            return false;
        }
        catch (Exception ex)
        {
            ConnectedDevice.CancelConnection();
            ConnectedDevice = null;
            await MauiAlertService.ShowAlertAsync("BLE", $"{ex.Message}");
        }

        return ConnectionState.IsConnected = true;
    }

    public async Task WriteCharacteristicAsync(BleMessage message)
    {
        CancellationTokenSource cts = new(TimeSpan.FromSeconds(3));

        if (ConnectedDevice == null)
        { throw new BleWriteFailedException("Device not connected"); }

        if (navChar == null)
        { throw new BleWriteFailedException("Navigation characteristic not found"); }

        try
        { await ConnectedDevice.WriteCharacteristicAsync(navChar, message.Data, false, cts.Token); }
        catch (TaskCanceledException)
        {
            await MauiAlertService.ShowAlertAsync("BLE", "Write operation timed out.");
            throw new BleWriteFailedException("Write operation timed out");
        }
        catch (InvalidOperationException ex)
        {
            await MauiAlertService.ShowAlertAsync("BLE", "Connection lost.");
            throw new BleWriteFailedException("BLE connection lost", ex);
        }
    }

    public async Task Disconnect()
    {
        ConnectedDevice?.CancelConnection();
        ConnectedDevice = null;
        ConnectionState.IsConnected = false;
        scanSub?.Dispose();
        scanSub = null;
    }
}

public class DeviceData : INotifyPropertyChanged
{
    IPeripheral? peripheral;
    public IPeripheral? Peripheral
    {
        get => peripheral;
        set
        {
            peripheral = value;
            OnChanged(nameof(Peripheral));
            OnChanged(nameof(IsEnabled));
        }
    }
    public string Name { get; set; }
    public string Id { get; }
    public int Rssi { get; }
    public string Details => $"RSSI {Rssi} | {Id}";
    public bool IsEnabled => Peripheral != null;

    public DeviceData(int rssi, string id, string name = "NO_NAME", IPeripheral? peripheral = null)
    {
        Rssi = rssi;
        Id = id;
        Name = name;
        Peripheral = peripheral;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnChanged(string n) => PropertyChanged?.Invoke(this, new(n));
}
