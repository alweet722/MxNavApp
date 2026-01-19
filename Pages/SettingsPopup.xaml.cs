using CommunityToolkit.Maui.Views;
using NBNavApp.ViewModels;

namespace NBNavApp.Pages;

public partial class SettingsPopup : Popup
{
	public SettingsPopup(SettingsPopupViewModel vm)
	{
		InitializeComponent();
        BindingContext = vm;
    }
}