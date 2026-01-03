namespace NBNavApp;

public interface IAlertInterface
{
    static Task ShowAlertAsync(string title, string message, string cancel = "Close") => throw new NotImplementedException();
}
