using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PulseLink.Services;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;

namespace PulseLink.ViewModels;

public record DeviceDisplay(string Name, string Id);

public partial class MainViewModel : ObservableObject
{
    private readonly IBluetoothService _ble;
    private readonly StreamService _stream;

    [ObservableProperty] 
    private int bpm = 0;

    [ObservableProperty] 
    private string status = "准备就绪";

    [ObservableProperty] 
    private string streamUrl;

    [ObservableProperty] 
    private bool isStreaming;

    public ObservableCollection<DeviceDisplay> Devices { get; } = new();

    // Dependency Injection Constructor
    public MainViewModel(IBluetoothService ble, StreamService stream)
    {
        _ble = ble;
        _stream = stream;
        StreamUrl = _stream.StreamUrl;

        _ble.StatusChanged += HandleStatusChange;

        _ble.HeartRateUpdated += val => 
        {
            Bpm = val;
            if (IsStreaming) _stream.SendBpmAsync(val);
        };
    }

    private void HandleStatusChange(string msg)
    {
        // Using Dispatcher to update UI collection from background thread
        Application.Current.Dispatcher.Invoke(() => 
        {
            if (msg.StartsWith("DISCOVERED:")) 
            {
                var info = msg.Replace("DISCOVERED:", "");
                var parts = info.Split('|');
                if (parts.Length == 2)
                {
                    Devices.Add(new DeviceDisplay(parts[0], parts[1]));
                }
            }
            else 
            {
                Status = msg;
            }
        });
    }

    [RelayCommand]
    public void Scan() 
    { 
        Devices.Clear(); 
        _ble.StartScan(); 
    }

    [RelayCommand]
    public void Connect(string id) 
    {
        _ble.ConnectAsync(id);
    }

    [RelayCommand]
    public async Task ToggleStream()
    {
        if (!IsStreaming) 
        { 
            Status = "正在初始化直播...";
            await _stream.StartAsync(); 
            IsStreaming = true; 
            Status = "直播已上线"; 
        }
    }
}