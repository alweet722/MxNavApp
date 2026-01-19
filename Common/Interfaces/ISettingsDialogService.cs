using NBNavApp.ViewModels;

namespace NBNavApp.Common.Interfaces;

public interface ISettingsDialogService
{
    Task<SettingsDialogResult?> ShowSettingsDialogAsync(string? mxNavName, Color? mxNavColor);
}
