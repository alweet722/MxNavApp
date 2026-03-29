using CommunityToolkit.Maui.Views;
using NBNavApp.ViewModels;

namespace NBNavApp.Pages;

public partial class SettingsPage : ContentPage
{
	public SettingsPage(SettingsPageViewModel vm)
	{
		InitializeComponent();
        BindingContext = vm;
    }
}