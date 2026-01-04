using NBNavApp.ViewModels;

namespace NBNavApp;

public partial class StartPage : ContentPage
{
    public StartPage(StartPageViewModel vm)
    {
        InitializeComponent();
        BindingContext = vm;
    }
}