using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PulseLink.Resources;
using PulseLink.Services;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;

namespace PulseLink.ViewModels;

public record DeviceDisplay(string Name, string Id);

public partial class MainViewModel : ObservableObject
{
    private readonly IBluetoothService _ble;
    private readonly LocalizationService _loc;
    private readonly StreamService _streamService;

    [ObservableProperty]
    private int bpm = 0;

    [ObservableProperty]
    private string status;

    [ObservableProperty]
    private bool isGhostMode = false;
    
    [ObservableProperty]
    private string localServerUrl = "Initializing server...";
    
    [ObservableProperty]
    private string mqttStreamUrl = "Initializing stream...";

    [ObservableProperty]
    private string userId = "Generating..."; // New property for UserId

    [ObservableProperty]
    private bool isConnected = false; // Tracks Bluetooth connection status

    public ObservableCollection<DeviceDisplay> Devices { get; } = new();

    public MainViewModel(IBluetoothService ble, LocalizationService loc, StreamService streamService)
    {
        _ble = ble;
        _loc = loc;
        _streamService = streamService;
        
        status = Strings.Status_Ready;

        _ble.StatusChanged += HandleStatusChange;
        _ble.HeartRateUpdated += val =>
        {
            Bpm = val;
            _ = _streamService.SendBpmAsync(val);
        };

        // Subscribe to StreamService's PropertyChanged event
        _streamService.PropertyChanged += StreamService_PropertyChanged;

        SetLocalServerUrl(Config.HttpServerBaseUrl);
        // Initialize MqttStreamUrl and UserId from StreamService
        MqttStreamUrl = _streamService.StreamUrl;
        UserId = _streamService.UserId;

        _ = _streamService.StartAsync();

        // Start initial scan immediately, no auto-connect
        _ = ScanCommand.ExecuteAsync(null);
    }

    private void StreamService_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StreamService.StreamUrl))
        {
            MqttStreamUrl = _streamService.StreamUrl;
        }
        if (e.PropertyName == nameof(StreamService.UserId))
        {
            UserId = _streamService.UserId;
        }
    }

    private void HandleStatusChange(string msg)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (msg.StartsWith("DISCOVERED:"))
            {
                var info = msg.Replace("DISCOVERED:", "");
                var parts = info.Split('|');
                if (parts.Length == 2)
                {
                    // Check if device already in list
                    if (!Devices.Any(d => d.Id == parts[1]))
                    {
                        Devices.Add(new DeviceDisplay(parts[0], parts[1]));
                    }
                }
            }
            else
            {
                Status = msg;
                if (msg == Strings.Status_Connected)
                {
                    IsConnected = true;
                }
                else if (msg == Strings.Status_Ready || msg.StartsWith(Strings.Status_Error_DeviceNotFound) || msg.StartsWith(Strings.Status_Error_Exception))
                {
                    IsConnected = false;
                }
            }
        });
    }

    private void SetLocalServerUrl(string baseUrl)
    {
        try
        {
            var ipv6Address = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                .FirstOrDefault(ip => ip.Address.AddressFamily == AddressFamily.InterNetworkV6 && 
                                       !ip.Address.IsIPv6LinkLocal &&
                                       ip.PrefixOrigin != PrefixOrigin.WellKnown &&
                                       ip.SuffixOrigin != SuffixOrigin.LinkLayerAddress &&
                                       ip.Address.ScopeId == 0);

            if (ipv6Address != null)
            {
                var port = new System.Uri(baseUrl).Port;
                LocalServerUrl = $"http://[{ipv6Address.Address}]:{port}";
            }
            else
            {
                LocalServerUrl = "No suitable IPv6 address found.";
            }
        }
        catch (Exception ex)
        {
            LocalServerUrl = $"Could not determine local IP: {ex.Message}";
        }
    }
    
    [RelayCommand]
    private async Task Connect(string deviceId)
    {
        await _ble.ConnectAsync(deviceId);
    }

    [RelayCommand]
    private async Task Disconnect()
    {
        await _ble.DisconnectAsync();
    }

    [RelayCommand]
    private async Task Scan()
    {
        await _ble.DisconnectAsync();
        Devices.Clear();
        _ble.StartScan();
        Status = Strings.Status_Scanning;

        // Give some time for devices to be discovered, then update status if none found
        await Task.Delay(TimeSpan.FromSeconds(5)); 
        if (!Devices.Any())
        {
            Status = Strings.Status_Error_DeviceNotFound;
        }
        else if(!IsConnected)
        {
            Status = "请选择一个设备..."; // Please select a device...
        }
    }

    [RelayCommand]
    private void ToggleGhostMode()
    {
        IsGhostMode = !IsGhostMode;
        Status = IsGhostMode ? "Ghost mode enabled" : "Ghost mode disabled";
    }

    [RelayCommand]
    private void SwitchLanguage(string culture)
    {
        _loc.SetLanguage(culture);
    }
}