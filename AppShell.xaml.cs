using NBNavApp.Pages;

namespace NBNavApp;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        Routing.RegisterRoute("RoutePage", typeof(RoutePage));
        Routing.RegisterRoute("SettingsPage", typeof(SettingsPage));
    }
}
