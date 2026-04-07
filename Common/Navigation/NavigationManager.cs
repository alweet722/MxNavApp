using NBNavApp.Common.Ble;
using NBNavApp.Common.Services;
using NBNavApp.Common.Util;

namespace NBNavApp.Common.Navigation;

public class NavigationManager
{
    readonly BleInterface bleInterface;
    readonly AppVisibilityService appVisibilityService;

    public bool IsNavigating { get; private set; }
    public bool ServiceStopRequested { get; set; }

    public NavigationManager(BleInterface bleInterface, AppVisibilityService appVisibilityService)
    {
        this.bleInterface = bleInterface;
        this.appVisibilityService = appVisibilityService;
    }

    public async Task StartNavigationAsync()
    {
        if (!bleInterface.BleConnectionState.IsConnected)
        { await MauiPopupService.ShowAlertAsync("BLE", "Connection lost."); }

        if (IsNavigating)
        { return; }

#if ANDROID31_0_OR_GREATER
        IsNavigating = true;
        await PlatformLocationService.StartAsync();
#endif
    }

    public void StopNavigation()
    {
        if (!IsNavigating)
        { return; }

#if ANDROID31_0_OR_GREATER
        if (appVisibilityService.IsInForeground)
        {
            System.Diagnostics.Debug.WriteLine("Stopping immediately");
            PlatformLocationService.Stop();
        }
        else
        {
            System.Diagnostics.Debug.WriteLine("Stop requested");
            ServiceStopRequested = true;
        }
        IsNavigating = false;
#endif
    }
}