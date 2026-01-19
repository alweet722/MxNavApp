using System.ComponentModel;
using System.Windows.Input;

namespace NBNavApp.ViewModels;

public sealed class SettingsDialogResult
{
    public string MxNavName { get; init; }
    public Color MxNavColor { get; init; }
}

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

    public ICommand BackCommand { get; }
    public ICommand SaveCommand { get; }

    public event PropertyChangedEventHandler? PropertyChanged;
    void OnPropertyChanged(string n) => PropertyChanged?.Invoke(this, new(n));

    public SettingsPopupViewModel(Color mxNavColor, Action<SettingsDialogResult?> close)
    {
        MxNavName = Preferences.Default.Get(Constants.DISPL_DEV_KEY, string.Empty);
        MxNavColor = mxNavColor;
        this.close = close;

        BackCommand = new Command(() =>
            close(new SettingsDialogResult
            {
                MxNavName = this.MxNavName,
                MxNavColor = this.MxNavColor
            }));

        SaveCommand = new Command(() =>
        {
            Preferences.Default.Set(Constants.DISPL_DEV_KEY, MxNavName);
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
    }
}
