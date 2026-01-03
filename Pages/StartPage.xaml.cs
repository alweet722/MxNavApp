using NBNavApp.ViewModels;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace NBNavApp;

public partial class StartPage : ContentPage
{
    readonly BleSender bleSender;
    readonly BleScanViewModel vm;
    readonly ObservableCollection<DeviceData> myDevice = new();

    DeviceData? selection;

    public StartPage(BleSender bleSender, BleScanViewModel vm)
    {
        InitializeComponent();
        this.bleSender = bleSender;
        BindingContext = this.vm = vm;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            await CheckAndRequestLocationPermission();
            await CheckAndRequestNotificationPermission();
        });
    }

    private static async Task<PermissionStatus> CheckAndRequestLocationPermission()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.LocationAlways>();
        if (status == PermissionStatus.Granted)
        { return status; }
        await Permissions.RequestAsync<Permissions.LocationAlways>();
        return status;
    }

    private static async Task<PermissionStatus> CheckAndRequestNotificationPermission()
    {
        var status = await Permissions.CheckStatusAsync<Permissions.PostNotifications>();
        if (status == PermissionStatus.Granted)
        { return status; }
        await Permissions.RequestAsync<Permissions.PostNotifications>();
        return status;
    }

    private async void OnConnectionToggleClicked(object sender, EventArgs e)
    {
        if (bleSender.ConnectedDevice != null)
        {
            await bleSender.Disconnect();

            ConnDvc.Text = "Disconnected";
            ConnectionToggleBtn.IsEnabled = false;
            ConnectionToggleBtn.Text = "Connect";
            NextPageBtn.IsEnabled = false;
        }
        else
        {
            if (selection == null || selection.Peripheral == null)
            {
                ConnectionToggleBtn.IsEnabled = false;
                return;
            }

            ConnectionToggleBtn.IsEnabled = false;

            var status = await bleSender.ConnectAndCacheCharacteristic(selection.Peripheral);
            if (!status)
            {
                await MauiAlertService.ShowAlertAsync("BLE", "Connection attempt failed");
                return;
            }

            if (!Preferences.Default.ContainsKey(Constants.DISPL_DEV_KEY) || string.IsNullOrEmpty(Preferences.Default.Get(Constants.DISPL_DEV_KEY, string.Empty)))
            { Preferences.Default.Set(Constants.DISPL_DEV_KEY, bleSender.ConnectedDevice?.Name); }

            if (vm.MyDevice.Count < 1)
            { vm.MyDevice.Add(selection); }

            ConnectionToggleBtn.Text = "Disconnect";
            ConnectionToggleBtn.IsEnabled = true;

            ConnDvc.Text = $"{bleSender.ConnectedDevice?.Name}";
            NextPageBtn.IsEnabled = true;

            MyDevice.SelectedItem = null;
            FoundDevices.SelectedItem = null;
        }
    }

    private async void OnNextPageClicked(object sender, EventArgs e)
    {
        if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
        {
            await MauiAlertService.ShowAlertAsync("Network", "No internet connection! Internet connection required for routing.");
            return;
        }
        await Shell.Current.GoToAsync("RoutePage");
    }

    private async void OnDeviceSelected(object sender, SelectionChangedEventArgs e)
    {
        CollectionView view = (CollectionView)sender;
        if (view.ClassId == "MyDevice")
        { FoundDevices.SelectedItem = null; }
        else if (view.ClassId == "FoundDevices")
        { MyDevice.SelectedItem = null; }

        selection = (DeviceData?)e.CurrentSelection.FirstOrDefault();

        if (selection == null || selection.Peripheral == null)
        {
            view.SelectedItem = null;
            return;
        }
        ConnectionToggleBtn.IsEnabled = true;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await vm.InitialScanAsync();
    }
}