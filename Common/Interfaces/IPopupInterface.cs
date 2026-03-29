namespace NBNavApp.Common.Interfaces;

public interface IPopupInterface
{
    static Task ShowAlertAsync(string title, string message, string cancel = "Close") => throw new NotImplementedException();
    static Task<bool> ShowAlertAsync(string title, string message, string accept, string cancel = "Close") => throw new NotImplementedException();
    static Task<string> ShowPromptAsync(string title, string message, string placeholder, string initialValue, string accept = "Done", string cancel = "Close") => throw new NotImplementedException();
}
