using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PulseLink.Resources;
using PulseLink.Services;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Windows;

namespace PulseLink.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly IBluetoothService _ble;
    private readonly LocalizationService _loc; // Inject LocalizationService
    
    [ObservableProperty]
    private int bpm = 0;

    [ObservableProperty]
    private string status;

    [ObservableProperty]
    private bool isGhostMode = false;
    
    [ObservableProperty]
    private string localServerUrl = "Initializing server...";

    public MainViewModel(IBluetoothService ble, HttpServerService httpServer, LocalizationService loc) // Add LocalizationService to constructor
    {
        _ble = ble;
        _loc = loc; // Assign LocalizationService
        status = Strings.Status_Ready;

        _ble.StatusChanged += HandleStatusChange;
        _ble.HeartRateUpdated += val => { Bpm = val; };

        SetLocalServerUrl(httpServer.ServerUrl);

        _ble.StartScan();
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
                                       ip.Address.ScopeId == 0); // Exclude addresses with scope IDs (like link-local)

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
        catch
        {
            LocalServerUrl = "Could not determine local IP.";
        }
    }


    private void HandleStatusChange(string msg)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (!msg.StartsWith("DISCOVERED:"))
            {
                Status = msg;
            }
        });
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