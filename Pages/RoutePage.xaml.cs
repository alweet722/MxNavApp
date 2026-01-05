using NBNavApp.ViewModels;

namespace NBNavApp;

public partial class RoutePage : ContentPage
{
    public RoutePage(RoutePageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}