using Shiny.BluetoothLE;

namespace NBNavApp.Common.Ble;

public class BleStateEventArgs : EventArgs
{
    public ConnectionState State { get; set; }
}

public delegate void BleStateMonitorEventHandler(object sender, BleStateEventArgs e);

public class BleStateMonitor : BleDelegate
{
    public event BleStateMonitorEventHandler? PeripheralStateChanged;
    public override Task OnPeripheralStateChanged(IPeripheral peripheral)
    {
        if (peripheral.Status == ConnectionState.Connected || peripheral.Status == ConnectionState.Disconnected)
        { PeripheralStateChanged?.Invoke(this, new() { State = peripheral.Status }); }
        return base.OnPeripheralStateChanged(peripheral);
    }
}