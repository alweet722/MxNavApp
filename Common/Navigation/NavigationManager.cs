using NBNavApp.Common.Ble;
using NBNavApp.Common.Util;

namespace NBNavApp.Common.Navigation;

public class NavigationManager
{
    readonly BleInterface bleInterface;

    public bool IsNavigating { get; private set; }

    public NavigationManager(BleInterface bleInterface)
    {
        this.bleInterface = bleInterface;
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
        PlatformLocationService.Stop();
        IsNavigating = false;
#endif
    }
}