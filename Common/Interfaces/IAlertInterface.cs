namespace NBNavApp.Common.Interfaces;

public interface IAlertInterface
{
    static Task ShowAlertAsync(string title, string message, string cancel = "Close") => throw new NotImplementedException();
}
