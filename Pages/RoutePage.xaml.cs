using NBNavApp.ViewModels;

namespace NBNavApp.Pages;

public partial class RoutePage : ContentPage
{
    public RoutePage(RoutePageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is ViewModels.RoutePageViewModel vm)
        {
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await vm.ReturnToStartPage();
            });
            return true;
        }

        return true;
    }
}