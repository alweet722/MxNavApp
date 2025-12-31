using Shiny.BluetoothLE;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace NBNavApp;

public partial class StartPage : ContentPage
{
    readonly IBleManager bleManager;
    readonly BleSender bleSender;

    DeviceData? selection;

    public ObservableCollection<DeviceData> FoundDevices { get; } = new();

    public StartPage(IBleManager bleManager, BleSender bleSender)
    {
        InitializeComponent();
        this.bleManager = bleManager;
        this.bleSender = bleSender;
        Devices.ItemsSource = FoundDevices;
    }

    private async void OnScanClicked(object sender, EventArgs e)
    {
        ScanBtn.IsEnabled = false;
        Shiny.AccessState access = await bleManager.RequestAccessAsync();
        if (access != Shiny.AccessState.Available)
        {
            await DisplayAlertAsync("BLE", $"No access: {access}", "Close");
            ScanBtn.IsEnabled = true;
            return;
        }

        FoundDevices.Clear();

        await bleSender.ScanDevicesAsync(bleManager, FoundDevices);

        ScanBtn.IsEnabled = true;
    }

    private async void OnConnectionToggleClicked(object sender, EventArgs e)
    {
        if (bleSender.ConnectedDevice != null)
        {
            await bleSender.Disconnect();

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

            ScanBtn.IsEnabled = true;
            ConnectionToggleBtn.IsEnabled = false;

            await bleSender.ConnectAndCacheCharacteristic(selection.Peripheral);
            ConnectionToggleBtn.Text = "Disconnect";
            ConnectionToggleBtn.IsEnabled = true;

            ConnDvc.Text = $"Connected to {bleSender.ConnectedDevice?.Name}";
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
}