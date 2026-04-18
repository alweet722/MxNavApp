using NBNavApp.Common.Messages;
using NBNavApp.Common.Util;
using Shiny.BluetoothLE;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace NBNavApp.Common.Ble;

public class BleInterface
{
    BleCharacteristicInfo? navChar;
    IDisposable? scanSub;

    readonly BleStateMonitor bleStateMonitor;

    const int writeRetryCount = 3;
    const int connectionRetryCount = 3;
    const int connectionTimeoutSeconds = 5;

    int writeAttempt = 0;

    public IBleManager BleManager { get; set; }
    public IPeripheral? ConnectedDevice { get; set; }
    public BleConnectionState BleConnectionState { get; set; }

    public event BleStateMonitorEventHandler? BleConnectionStateChanged;

    public BleInterface(IBleManager bleManager, BleConnectionState bleConnectionState, BleStateMonitor bleStateMonitor)
    {
        BleConnectionState = bleConnectionState;
        BleManager = bleManager;
        this.bleStateMonitor = bleStateMonitor;

        bleStateMonitor.PeripheralStateChanged += OnPeripheralStateChanged;
    }

    private void OnPeripheralStateChanged(object sender, BleStateEventArgs e)
    {
        BleConnectionState.IsConnected = e.State == ConnectionState.Connected;
        ConnectedDevice = e.ConnectedDevice;
        BleConnectionStateChanged?.Invoke(this, e);
    }

    public async Task<List<DeviceData>> ScanDevicesAsync(ObservableCollection<DeviceData>? table = null, int timeout = 10)
    {
        List<DeviceData> peripherals = new();

        if (timeout < 0)
        { return peripherals; }

        if (BleManager.IsScanning)
        {
            await MauiPopupService.ShowAlertAsync("BLE", "Another scan already in progress.");
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

        for (int attempt = 1; attempt <= connectionRetryCount + 1; ++attempt)
        {
            using CancellationTokenSource cts = new(TimeSpan.FromSeconds(connectionTimeoutSeconds));
            try
            {
                await peripheral.ConnectAsync(null, cts.Token);
                break;
            }
            catch (TaskCanceledException)
            {
                if (attempt > connectionRetryCount)
                {
                    await MauiPopupService.ShowAlertAsync("BLE", $"Connection timed out after {connectionRetryCount} retries.");
                    return false;
                }
                await Task.Delay(500 * attempt);
            }
            catch (Exception ex)
            {
                if (attempt > connectionRetryCount)
                {
                    await MauiPopupService.ShowAlertAsync("BLE", $"Connection failed: {ex.Message}");
                    return false;
                }
                await Task.Delay(500 * attempt);
            }
        }

        using CancellationTokenSource charCts = new(TimeSpan.FromSeconds(connectionTimeoutSeconds));
        try
        {
            navChar = await peripheral.GetCharacteristicAsync(Constants.SERVICE_UUID, Constants.NAV_UUID, charCts.Token);
            return BleConnectionState.IsConnected;
        }
        catch (TaskCanceledException)
        {
            peripheral.CancelConnection();
            await MauiPopupService.ShowAlertAsync("BLE", "Timeout while getting navigation characteristic.");
            return false;
        }
        catch (Exception ex)
        {
            peripheral.CancelConnection();
            await MauiPopupService.ShowAlertAsync("BLE", $"Failed to get characteristic: {ex.Message}");
            return false;
        }
    }

    public async Task WriteCharacteristicAsync(BleMessage message)
    {
        if (ConnectedDevice == null)
        { throw new BleWriteFailedException("Device not connected"); }

        if (navChar == null)
        { throw new BleWriteFailedException("Navigation characteristic not found"); }

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(3));
        try
        {
            ++writeAttempt;
            await ConnectedDevice.WriteCharacteristicAsync(navChar, message.Data, false, cts.Token);
            writeAttempt = 0;
            return;
        }
        catch (TaskCanceledException)
        {
            if (writeAttempt > writeRetryCount)
            {
                await MauiPopupService.ShowAlertAsync("BLE", "Write operation timed out.");
                throw new BleWriteFailedException($"Write operation timed out after {writeRetryCount} retries");
            }
            await Task.Delay(100 * writeAttempt);
        }
        catch (InvalidOperationException ex)
        {
            await MauiPopupService.ShowAlertAsync("BLE", "Connection lost.");
            throw new BleWriteFailedException("BLE connection lost", ex);
        }
        catch (Exception ex)
        {
            if (writeAttempt > writeRetryCount)
            { throw new BleWriteFailedException($"Write failed after {writeRetryCount} attempts: {ex.Message}", ex); }

            await Task.Delay(100 * writeAttempt);
        }
    }

    public async Task Disconnect()
    {
        ConnectedDevice?.CancelConnection();
        scanSub?.Dispose();
        scanSub = null;
        navChar = null;
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
