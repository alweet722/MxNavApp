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
        if (BindingContext is ViewModels.RoutePageViewModel vm)
        { _ = vm.ReturnToStartPage(); }

        return base.OnBackButtonPressed();
    }
}