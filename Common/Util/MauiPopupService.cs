using NBNavApp.Common.Interfaces;

namespace NBNavApp.Common.Util;

internal class MauiPopupService : IPopupInterface
{
    public static Task ShowAlertAsync(string title, string message, string cancel = "Close")
    {
        return MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = Application.Current?.Windows[0].Page;
            if (page == null)
            { return; }

            if (page is Shell shell)
            { page = shell.CurrentPage ?? shell; }

            await page.DisplayAlertAsync(title, message, cancel);
        });
    }

    public static Task<bool> ShowAlertAsync(string title, string message, string accept, string cancel)
    {
        return MainThread.InvokeOnMainThreadAsync(async Task<bool> () =>
        {
            var page = Application.Current?.Windows[0].Page;
            if (page == null)
            { return false; }

            if (page is Shell shell)
            { page = shell.CurrentPage ?? shell; }

            return await page.DisplayAlertAsync(title, message, accept, cancel);
        });
    }

    public static Task<string?> ShowPromptAsync(string title, string message, string initialValue, string accept = "Done", string cancel = "Close", string? placeholder = null)
    {
        return MainThread.InvokeOnMainThreadAsync(async Task<string?> () =>
        {
            var page = Application.Current?.Windows[0].Page;
            if (page == null)
            { return null; }

            if (page is Shell shell)
            { page = shell.CurrentPage ?? shell; }

            return await page.DisplayPromptAsync(title, message, accept, cancel, placeholder, initialValue: initialValue);
        });
    }
}
