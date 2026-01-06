using NBNavApp.ViewModels;

namespace NBNavApp;

public partial class RoutePage : ContentPage
{
    public RoutePage(RoutePageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }

    protected override bool OnBackButtonPressed()
    {
        if (BindingContext is ViewModels.RoutePageViewModel vm && vm.BackButtonCommand != null)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                if (vm.BackButtonCommand.CanExecute(null))
                { vm.BackButtonCommand.Execute(null); }
            });

            return true;
        }

        return base.OnBackButtonPressed();
    }
}