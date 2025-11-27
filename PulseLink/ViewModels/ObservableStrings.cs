using PulseLink.Resources;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PulseLink.ViewModels;

public class ObservableStrings : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public Strings Strings { get; } = new();

    public void Refresh()
    {
        OnPropertyChanged(nameof(Strings));
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}