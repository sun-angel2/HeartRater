using PulseLink.Resources;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace PulseLink.ViewModels;

public class ObservableStrings : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public Strings Strings => new Strings(); // A new instance to ensure property changed works with new culture info.

    public void Refresh()
    {
        OnPropertyChanged(nameof(Strings));
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}