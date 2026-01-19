using System.ComponentModel;

namespace NBNavApp.ViewModels;

public sealed class SettingsDialogResult
{
    public string MxNavName { get; init; }
    public Color MxNavColor { get; init; }
}

public class SettingsPopupViewModel : INotifyPropertyChanged
{
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

    Color? mxNavColor;
    public Color? MxNavColor
    {
        get => mxNavColor;
        set
        {
            if (mxNavColor == value) return;
            mxNavColor = value;
            OnPropertyChanged(nameof(MxNavColor));
        }

    }

    private readonly Action<SettingsDialogResult?> close;

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new(n));

    public SettingsPopupViewModel(string mxNavName, Color mxNavColor, Action<SettingsDialogResult?> close)
    {
        MxNavName = mxNavName;
        MxNavColor = mxNavColor;
        this.close = close;
    }

    private void NotifyUI()
    {
        OnPropertyChanged(nameof(MxNavName));
        OnPropertyChanged(nameof(MxNavColor)); 
    }
}
