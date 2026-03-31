using Shiny.BluetoothLE;

namespace NBNavApp.Common.Ble;

public class BleStateEventArgs : EventArgs
{
    public ConnectionState State { get; set; }
    public IPeripheral? ConnectedDevice { get; set; }
}

public delegate void BleStateMonitorEventHandler(object sender, BleStateEventArgs e);

public class BleStateMonitor : BleDelegate
{
    public event BleStateMonitorEventHandler? PeripheralStateChanged;
    public override Task OnPeripheralStateChanged(IPeripheral peripheral)
    {
        PeripheralStateChanged?.Invoke(this, new() 
        { 
            State = peripheral.Status,
            ConnectedDevice = peripheral.Status == ConnectionState.Connected ? peripheral : null
        });
        return base.OnPeripheralStateChanged(peripheral);
    }
}