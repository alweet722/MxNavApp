using CommunityToolkit.Maui.Extensions;
using NBNavApp.Common.Interfaces;
using NBNavApp.Pages;
using NBNavApp.ViewModels;

namespace NBNavApp.Common.Util;

internal class SettingsDialogService : ISettingsDialogService
{
    public async Task<SettingsDialogResult?> ShowSettingsDialogAsync(string? mxNavName, Color? mxNavColor)
    {
        var page = Application.Current?.MainPage;
        if (page is null)
        { return null; }

        SettingsPopup? settingsPopup = null;
        SettingsDialogResult? dialogResult = null;

        var vm = new SettingsPopupViewModel(
            mxNavName,
            mxNavColor,
            close: async r =>
            {
                dialogResult = r;
                await settingsPopup?.CloseAsync();
            });

        settingsPopup = new();

        await page.ShowPopupAsync(settingsPopup);

        return dialogResult;
    }


}
