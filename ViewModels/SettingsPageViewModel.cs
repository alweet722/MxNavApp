using NBNavApp.Common.Ble;
using NBNavApp.Common.Messages.ParameterMessages;
using NBNavApp.Common.Util;
using System.ComponentModel;
using System.Windows.Input;

namespace NBNavApp.ViewModels;

public sealed class SettingsDialogResult
{
    public string MxNavName { get; init; } = string.Empty;
    public ColorEntry? MxNavColor { get; init; }
}

public record ColorEntry(string Name, Color Color);

public class SettingsPageViewModel : INotifyPropertyChanged
{
    readonly BleSender bleSender;
    readonly BleConnectionState bleConnectionState;

    string? apiKey;
    public string? ApiKey
    {
        get => apiKey;
        set
        {
            if (apiKey == value) return;
            apiKey = value;
            OnPropertyChanged(nameof(ApiKey));
        }
    }

    string? mxNavName;
    public string? MxNavName
    {
        get => mxNavName;
        set
        {
            if (mxNavName == value) return;
            mxNavName = value;
            OnPropertyChanged(nameof(MxNavName));
        }
    }

    ColorEntry? mxNavColor;
    public ColorEntry? MxNavColor
    {
        get => mxNavColor;
        set
        {
            if (mxNavColor == value) return;
            mxNavColor = value;
            OnPropertyChanged(nameof(MxNavColor));
        }
    }

    bool isSaved;
    public bool IsSaved
    {
        get => isSaved;
        set
        {
            if (isSaved == value) return;
            isSaved = value;
            OnPropertyChanged(nameof(IsSaved));
        }
    }

    public string SaveStatusText => IsSaved ? "Saved!" : string.Empty;

    public List<ColorEntry> Colors { get; } = new()
    {
        { new("Dashboard green", new Color(180, 250, 0))},
        { new("Dashboard red", new Color(220, 0, 0))},
    };

    string favDeviceName;

    public ICommand EditApiKeyCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand SaveCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new(n));

    public SettingsPageViewModel(BleSender bleSender, BleConnectionState bleConnectionState)
    {
        this.bleSender = bleSender;
        this.bleConnectionState = bleConnectionState;

        var colorName = Preferences.Default.Get(Constants.MX_NAV_COLOR_KEY, string.Empty);
        ApiKey = Preferences.Default.Get(Constants.API_KEY_KEY, string.Empty);
        favDeviceName = MxNavName = Preferences.Default.Get(Constants.MX_NAV_NAME_KEY, string.Empty);
        MxNavColor = !string.IsNullOrEmpty(colorName) ? Colors.FirstOrDefault(d => d.Name == colorName) : Colors[0];

        EditApiKeyCommand = new Command(async () =>
        {
            string? setApiKey = await MauiPopupService.ShowPromptAsync("ORS API key", "Enter your ORS API key:", ApiKey);
            if (setApiKey == null && string.IsNullOrEmpty(ApiKey))
            { await MauiPopupService.ShowAlertAsync("ORS API key", "ORS API key was not set."); }
        });

        BackCommand = new Command(async () =>
        {
            if (bleConnectionState.IsConnected)
            {
                if (MxNavColor != null)
                {
                    ColorMessage colorMessage = new(MxNavColor.Color);
                    try
                    { await bleSender.WriteCharacteristicAsync(colorMessage); }
                    catch (BleWriteFailedException)
                    { return; }
                }

                if (!string.IsNullOrEmpty(MxNavName) && MxNavName != favDeviceName)
                {
                    foreach (var msg in NameMessage.CreateNameMessages(MxNavName))
                    {
                        try
                        { await bleSender.WriteCharacteristicAsync(msg); }
                        catch (BleWriteFailedException)
                        { return; }
                    }
                    favDeviceName = MxNavName;

                    await bleSender.Disconnect();
                    await bleSender.ScanDevicesAsync(timeout: 5);
                }
            }

            await Shell.Current.GoToAsync("..");
        });

        SaveCommand = new Command(() =>
        {
            Preferences.Default.Set(Constants.API_KEY_KEY, ApiKey);
            Preferences.Default.Set(Constants.MX_NAV_NAME_KEY, MxNavName);
            Preferences.Default.Set(Constants.MX_NAV_COLOR_KEY, MxNavColor?.Name);
            IsSaved = true;
            NotifyUI();
        });

        NotifyUI();
    }

    private void NotifyUI()
    {
        OnPropertyChanged(nameof(ApiKey));
        OnPropertyChanged(nameof(MxNavName));
        OnPropertyChanged(nameof(MxNavColor));
        OnPropertyChanged(nameof(IsSaved));
        OnPropertyChanged(nameof(SaveStatusText));
        OnPropertyChanged(nameof(Colors));
    }
}
