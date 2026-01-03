using Shiny.BluetoothLE;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;

namespace NBNavApp.ViewModels;

public class BleScanViewModel : INotifyPropertyChanged
{
    readonly BleSender bleSender;

    public ObservableCollection<DeviceData> FoundDevices { get; } = new();
    public ObservableCollection<DeviceData> MyDevice { get; } = new();
    public ICommand ScanCommand { get; set; }

    bool isScanning;
    public bool IsScanning
    {
        get => isScanning;
        set
        {
            if (isScanning == value) return;
            isScanning = value;
            PropertyChanged?.Invoke(this, new(nameof(IsScanning)));
        }
    }

    string fav;

    public event PropertyChangedEventHandler? PropertyChanged;

    public BleScanViewModel(BleSender bleSender)
    {
        this.bleSender = bleSender;

        fav = Preferences.Default.Get(Constants.DISPL_DEV_KEY, string.Empty);
        if (Preferences.Default.ContainsKey(Constants.DISPL_DEV_KEY) && !string.IsNullOrEmpty(fav))
        {
            DeviceData? dev = new(0, "N/A", fav);
            MyDevice.Add(dev);
        }

        ScanCommand = new Command(async () =>
        {
            Shiny.AccessState access = await bleSender.BleManager.RequestAccessAsync();
            if (access != Shiny.AccessState.Available)
            {
                IsScanning = false;
                await MauiAlertService.ShowAlertAsync("BLE", $"No access: {access}");
                return;
            }

            FoundDevices.Clear();

            await bleSender.ScanDevicesAsync(FoundDevices, 5);

            IsScanning = false;
        });
    }

    public async Task InitialScanAsync()
    {
        List<DeviceData> devices = await bleSender.ScanDevicesAsync(timeout: 2);
        foreach (DeviceData d in devices)
        { FoundDevices.Add(d); }

        DeviceData? dev = devices.FirstOrDefault(d => d.Name == fav);

        if (dev == null)
        { return; }

        MyDevice.Clear();
        MyDevice.Add(dev);
    }
}
