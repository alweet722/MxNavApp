using System.ComponentModel;
using System.Windows.Input;

namespace NBNavApp.ViewModels;

public sealed class SettingsDialogResult
{
    public string MxNavName { get; init; }
    public ColorEntry? MxNavColor { get; init; }
}

public record ColorEntry(string Name, Color Color);

public class SettingsPopupViewModel : INotifyPropertyChanged
{
    private readonly Action<SettingsDialogResult?> close;

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

    public ICommand BackCommand { get; }
    public ICommand SaveCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new(n));

    public SettingsPopupViewModel(Action<SettingsDialogResult?> close)
    {
        var colorName = Preferences.Default.Get(Constants.MX_NAV_COLOR_KEY, string.Empty);
        MxNavName = Preferences.Default.Get(Constants.MX_NAV_NAME_KEY, string.Empty);
        MxNavColor = !string.IsNullOrEmpty(colorName) ? Colors.FirstOrDefault(d => d.Name == colorName) : Colors[0];
        this.close = close;

        BackCommand = new Command(() =>
            close(new SettingsDialogResult
            {
                MxNavName = this.MxNavName,
                MxNavColor = this.MxNavColor
            }));

        SaveCommand = new Command(() =>
        {
            Preferences.Default.Set(Constants.MX_NAV_NAME_KEY, MxNavName);
            Preferences.Default.Set(Constants.MX_NAV_COLOR_KEY, MxNavColor?.Name);
            IsSaved = true;
            NotifyUI();
        });

        NotifyUI();
    }

    private void NotifyUI()
    {
        OnPropertyChanged(nameof(MxNavName));
        OnPropertyChanged(nameof(MxNavColor));
        OnPropertyChanged(nameof(IsSaved));
        OnPropertyChanged(nameof(SaveStatusText));
        OnPropertyChanged(nameof(Colors));
    }
}
