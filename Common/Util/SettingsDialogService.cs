using CommunityToolkit.Maui.Extensions;
using NBNavApp.Common.Interfaces;
using NBNavApp.Pages;
using NBNavApp.ViewModels;

namespace NBNavApp.Common.Util;

internal class SettingsDialogService : ISettingsDialogService
{
    public async Task<SettingsDialogResult?> ShowSettingsDialogAsync(string? mxNavName, ColorEntry? mxNavColor)
    {
        var page = Application.Current?.MainPage;
        if (page is null)
        { return null; }

        SettingsPopup? settingsPopup = null;
        SettingsDialogResult? dialogResult = null;

        var vm = new SettingsPopupViewModel(
            close: async r =>
            {
                dialogResult = r;
                settingsPopup?.CloseAsync();
            });

        settingsPopup = new(vm);

        await page.ShowPopupAsync(settingsPopup);

        return dialogResult;
    }
}
