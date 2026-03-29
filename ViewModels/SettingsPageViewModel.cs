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
            ((Command)ApplyCommand).ChangeCanExecute();
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
            ((Command)ApplyCommand).ChangeCanExecute();
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
            ((Command)ApplyCommand).ChangeCanExecute();
        }
    }

    bool isConnected;
    public bool IsConnected
    {
        get => isConnected;
        set
        {
            if (isConnected == value) return;
            isConnected = value;
            OnPropertyChanged(nameof(IsConnected));
        }
    }

    public List<ColorEntry> Colors { get; } = new()
    {
        { new("Dashboard green", new Color(180, 250, 0))},
        { new("Dashboard red", new Color(220, 0, 0))},
    };

    public ICommand AppearingCommand { get; }
    public ICommand EditApiKeyCommand { get; }
    public ICommand BackCommand { get; }
    public ICommand ApplyCommand { get; }

    string? originalApiKey;
    string? originalMxNavName;
    ColorEntry? originalMxNavColor;

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new(n));

    public SettingsPageViewModel(BleSender bleSender, BleConnectionState bleConnectionState)
    {
        this.bleSender = bleSender;
        this.bleConnectionState = bleConnectionState;

        AppearingCommand = new Command(() => OnAppearing());

        EditApiKeyCommand = new Command(async () =>
        {
            string? setApiKey = await MauiPopupService.ShowPromptAsync("ORS API key", "Enter your ORS API key:", ApiKey);
            if (setApiKey == null)
            { return; }
            ApiKey = setApiKey;
        });

        BackCommand = new Command(async () =>
        {
            await Shell.Current.GoToAsync("..");
        });

        ApplyCommand = new Command(
            execute: async () =>
        {
            Preferences.Default.Set(Constants.API_KEY_KEY, ApiKey);
            Preferences.Default.Set(Constants.MX_NAV_NAME_KEY, MxNavName);
            Preferences.Default.Set(Constants.MX_NAV_COLOR_KEY, MxNavColor?.Name);

            if (ApiKey != originalApiKey)
            { originalApiKey = ApiKey; }

            NotifyUI();

            if (!bleConnectionState.IsConnected)
            { return; }

            if (MxNavColor != null && MxNavColor != originalMxNavColor)
            {
                ColorMessage colorMessage = new(MxNavColor.Color);
                try
                { await bleSender.WriteCharacteristicAsync(colorMessage); }
                catch (BleWriteFailedException)
                { return; }

                originalMxNavColor = MxNavColor;
            }

            if (!string.IsNullOrEmpty(MxNavName) && MxNavName != originalMxNavName)
            {
                foreach (var msg in NameMessage.CreateNameMessages(MxNavName))
                {
                    try
                    { await bleSender.WriteCharacteristicAsync(msg); }
                    catch (BleWriteFailedException)
                    { return; }
                }
                originalMxNavName = MxNavName;

                await bleSender.Disconnect();
                await bleSender.ScanDevicesAsync(timeout: 5);

            }

            NotifyUI();
        },
            canExecute: () => ApiKey != originalApiKey || MxNavName != originalMxNavName || MxNavColor != originalMxNavColor
        );

        var colorName = Preferences.Default.Get(Constants.MX_NAV_COLOR_KEY, string.Empty);
        ApiKey = Preferences.Default.Get(Constants.API_KEY_KEY, string.Empty);
        MxNavName = Preferences.Default.Get(Constants.MX_NAV_NAME_KEY, string.Empty);
        MxNavColor = !string.IsNullOrEmpty(colorName) ? Colors.FirstOrDefault(d => d.Name == colorName) : Colors[0];

        originalApiKey = ApiKey;
        originalMxNavName = MxNavName;
        originalMxNavColor = MxNavColor;

        NotifyUI();
    }

    private void OnAppearing() => IsConnected = bleConnectionState.IsConnected;

    private void NotifyUI()
    {
        OnPropertyChanged(nameof(ApiKey));
        OnPropertyChanged(nameof(MxNavName));
        OnPropertyChanged(nameof(MxNavColor));
        OnPropertyChanged(nameof(Colors));
        ((Command)ApplyCommand).ChangeCanExecute();
    }
}
