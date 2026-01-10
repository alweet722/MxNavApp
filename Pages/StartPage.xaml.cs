using NBNavApp.ViewModels;

namespace NBNavApp;

public partial class StartPage : ContentPage
{
    bool _promptOpen;

    public StartPage(StartPageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override bool OnBackButtonPressed()
    {
        if (_promptOpen)
        { return true; }

        _promptOpen = true;
        MainThread.BeginInvokeOnMainThread(async () =>
        {
            if (BindingContext is StartPageViewModel vm)
            {
                try
                {
                    if (!await MauiAlertService.ShowAlertAsync("MX Navigation", "Do you really want to quit?", "Yes", "No"))
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
                    _promptOpen = false;
                }
            }
        });
        return true;
    }
}