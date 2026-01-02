namespace NBNavApp;

internal class MauiAlertService : IAlertInterface
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
}
