using System.ComponentModel;

namespace NBNavApp.Common.Ble;

public class BleConnectionState : INotifyPropertyChanged
{
    bool isConnected;
    public bool IsConnected
    {
        get => isConnected;
        set
        {
            if (isConnected == value) return;
            isConnected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsConnected)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
