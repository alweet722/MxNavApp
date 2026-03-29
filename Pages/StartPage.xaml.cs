using NBNavApp.Common.Util;
using NBNavApp.ViewModels;

namespace NBNavApp.Pages;

public partial class StartPage : ContentPage
{
    bool promptOpen;

    public StartPage(StartPageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override bool OnBackButtonPressed()
    {
        if (promptOpen)
        { return true; }

        promptOpen = true;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (BindingContext is StartPageViewModel vm)
            {
                try
                {
                    if (!await MauiPopupService.ShowAlertAsync("MX Navigation", "Do you really want to quit?", "Yes", "No"))
                    { return; }
                    _ = vm.DisposeAsync();
#if ANDROID
                    Platform.CurrentActivity.Finish();
#else
                    System.Diagnostics.Process.GetCurrentProcess().CloseMainWindow();
#endif
                }
                finally
                {
                    promptOpen = false;
                }
            }
        });
        return true;
    }
}