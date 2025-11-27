---

# PulseLink Project Generation Manifest

**Role:** You are an expert .NET/C# Developer agent.
**Objective:** Generate a complete, ready-to-run WPF application named "PulseLink" based on the specifications below.
**Tech Stack:** .NET 8.0, WPF, MVVM (CommunityToolkit), Dependency Injection, MQTTnet.

## 🚀 Execution Instructions for AI Agent

1.  **Initialize Project**: Run the shell commands in **Section 1** to create the solution, project, and install NuGet packages.
2.  **Create Files**: Iteratively create the files listed in **Section 2**. Ensure the file paths and contents match exactly.
3.  **Build**: After file creation, run `dotnet build` to verify success.

---

## Section 1: Project Initialization & Dependencies

Execute the following commands in your terminal (PowerShell or Bash) to set up the environment:

```powershell
# 1. Create Solution and Project
dotnet new sln -n PulseLink
dotnet new wpf -n PulseLink -f net8.0-windows
dotnet sln add PulseLink/PulseLink.csproj

# 2. Update Project File for Windows SDK (Required for Bluetooth)
# Note: We will overwrite the .csproj file in Section 2, but we need the project structure first.

# 3. Install NuGet Packages
cd PulseLink
dotnet add package CommunityToolkit.Mvvm
dotnet add package Microsoft.Extensions.DependencyInjection
dotnet add package MQTTnet
cd ..
```

---

## Section 2: File Generation

Please generate the following files with the specified paths and content.

### 1. Project Configuration (`PulseLink/PulseLink.csproj`)
*Updates the TargetFramework to support Windows Runtime APIs.*

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWPF>true</UseWPF>
    <ApplicationIcon />
    <StartupObject />
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="8.0.0" />
    <PackageReference Include="MQTTnet" Version="4.3.3.952" />
  </ItemGroup>

</Project>
```

### 2. Bluetooth Service Interface (`PulseLink/Services/IBluetoothService.cs`)

```csharp
using System;
using System.Threading.Tasks;

namespace PulseLink.Services;

public interface IBluetoothService
{
    event Action<int> HeartRateUpdated;
    event Action<string> StatusChanged;
    void StartScan();
    Task ConnectAsync(string deviceId);
}
```

### 3. Bluetooth Service Implementation (`PulseLink/Services/BluetoothService.cs`)

```csharp
using System;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;

namespace PulseLink.Services;

public class BluetoothService : IBluetoothService
{
    private DeviceWatcher? _watcher;
    public event Action<int>? HeartRateUpdated;
    public event Action<string>? StatusChanged;

    public void StartScan()
    {
        // Request specific properties to identify BLE devices
        string[] props = { "System.Devices.Aep.DeviceAddress", "System.Devices.Aep.IsConnected" };
        
        // Filter for devices that support Bluetooth LE
        _watcher = DeviceInformation.CreateWatcher(
            "(System.Devices.Aep.ProtocolId:=\"{bb7bb05e-5972-42b5-94fc-76eaa7084d49}\")", 
            props, 
            DeviceInformationKind.AssociationEndpoint);
        
        _watcher.Added += (s, a) => {
            if (!string.IsNullOrEmpty(a.Name)) 
            {
                // Format: Name|ID
                StatusChanged?.Invoke($"DISCOVERED:{a.Name}|{a.Id}");
            }
        };
        
        _watcher.Start();
        StatusChanged?.Invoke("SCANNING...");
    }

    public async Task ConnectAsync(string deviceId)
    {
        if (_watcher != null)
        {
            _watcher.Stop();
        }
        
        StatusChanged?.Invoke("CONNECTING...");
        
        try
        {
            var device = await BluetoothLEDevice.FromIdAsync(deviceId);
            if (device == null) 
            {
                StatusChanged?.Invoke("ERROR: DEVICE NOT FOUND");
                return;
            }

            // Get Heart Rate Service (UUID 0x180D)
            var services = await device.GetGattServicesForUuidAsync(GattServiceUuids.HeartRate);
            if (services.Status == GattCommunicationStatus.Success)
            {
                // Get Measurement Characteristic (UUID 0x2A37)
                var charResult = await services.Services[0].GetCharacteristicsForUuidAsync(GattCharacteristicUuids.HeartRateMeasurement);
                if (charResult.Status == GattCommunicationStatus.Success)
                {
                    var characteristic = charResult.Characteristics[0];
                    
                    // Subscribe to notifications
                    characteristic.ValueChanged += (s, args) =>
                    {
                        var reader = Windows.Storage.Streams.DataReader.FromBuffer(args.CharacteristicValue);
                        byte flags = reader.ReadByte();
                        // Check flag bit 0: 0 = UINT8, 1 = UINT16
                        int bpm = (flags & 1) == 0 ? reader.ReadByte() : reader.ReadUInt16();
                        HeartRateUpdated?.Invoke(bpm);
                    };
                    
                    await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify);
                    StatusChanged?.Invoke("CONNECTED");
                }
                else
                {
                    StatusChanged?.Invoke("ERROR: NO HR CHARACTERISTIC");
                }
            }
            else 
            {
                StatusChanged?.Invoke("ERROR: PLEASE ENABLE HR BROADCAST ON WATCH");
            }
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke($"ERROR: {ex.Message}");
        }
    }
}
```

### 4. Stream Service (`PulseLink/Services/StreamService.cs`)

```csharp
using MQTTnet;
using MQTTnet.Client;
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace PulseLink.Services;

public class StreamService
{
    private readonly IMqttClient _client;
    private readonly string _userId;

    public StreamService()
    {
        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();
        _userId = Guid.NewGuid().ToString("N").Substring(0, 8); // Generate short random ID
    }

    public string StreamUrl => $"https://your-github-username.github.io/PulseLink/?id={_userId}";

    public async Task StartAsync()
    {
        if (_client.IsConnected) return;

        var options = new MqttClientOptionsBuilder()
            .WithTcpServer("broker.emqx.io", 1883)
            .WithClientId($"PulseLink_{_userId}")
            // Last Will and Testament: Notify offline status if crashed
            .WithWillTopic($"pulselink/{_userId}/status")
            .WithWillPayload("offline")
            .WithWillRetain(true)
            .Build();

        await _client.ConnectAsync(options);
        
        // Announce online status
        await _client.PublishStringAsync($"pulselink/{_userId}/status", "online", retain: true);
    }

    public async Task SendBpmAsync(int bpm)
    {
        if (_client.IsConnected)
        {
            var payload = JsonSerializer.Serialize(new { bpm, timestamp = DateTime.UtcNow });
            await _client.PublishStringAsync($"pulselink/{_userId}/data", payload);
        }
    }
}
```

### 5. View Model (`PulseLink/ViewModels/MainViewModel.cs`)

```csharp
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
    private string status = "READY";

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
            Status = "INITIALIZING STREAM...";
            await _stream.StartAsync(); 
            IsStreaming = true; 
            Status = "LIVESTREAM ONLINE"; 
        }
    }
}
```

### 6. App Resources / Styles (`PulseLink/App.xaml`)

```xml
<Application x:Class="PulseLink.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Startup="OnStartup">
    <Application.Resources>
        <!-- Cyberpunk Color Palette -->
        <SolidColorBrush x:Key="NeonGreen" Color="#00FF41"/>
        <SolidColorBrush x:Key="DarkBg" Color="#0D0D0D"/>
        <SolidColorBrush x:Key="GlassBg" Color="#CC0D0D0D"/>

        <!-- Cyber Button Style -->
        <Style TargetType="Button">
            <Setter Property="Background" Value="Transparent"/>
            <Setter Property="Foreground" Value="{StaticResource NeonGreen}"/>
            <Setter Property="BorderBrush" Value="{StaticResource NeonGreen}"/>
            <Setter Property="BorderThickness" Value="1"/>
            <Setter Property="FontFamily" Value="Consolas"/>
            <Setter Property="Cursor" Value="Hand"/>
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="bd" 
                                Background="{TemplateBinding Background}" 
                                BorderBrush="{TemplateBinding BorderBrush}" 
                                BorderThickness="1" 
                                CornerRadius="3">
                            <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center" Margin="10,5"/>
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="bd" Property="Background" Value="#2200FF41"/>
                                <Setter TargetName="bd" Property="Effect">
                                    <Setter.Value>
                                        <DropShadowEffect Color="#00FF41" BlurRadius="10" ShadowDepth="0"/>
                                    </Setter.Value>
                                </Setter>
                            </Trigger>
                            <Trigger Property="IsPressed" Value="True">
                                <Setter TargetName="bd" Property="RenderTransform">
                                    <Setter.Value>
                                        <ScaleTransform ScaleX="0.95" ScaleY="0.95" CenterX="0.5" CenterY="0.5"/>
                                    </Setter.Value>
                                </Setter>
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </Application.Resources>
</Application>
```

### 7. Main Window UI (`PulseLink/MainWindow.xaml`)

```xml
<Window x:Class="PulseLink.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="PulseLink" 
        Height="320" Width="260"
        Background="{StaticResource GlassBg}" 
        WindowStyle="None" 
        AllowsTransparency="True"
        Topmost="True" 
        ResizeMode="NoResize" 
        MouseLeftButtonDown="Window_MouseDown">

    <Border BorderBrush="{StaticResource NeonGreen}" BorderThickness="1" CornerRadius="5" Margin="5">
        <Grid Margin="10">
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto"/>
                <RowDefinition Height="*"/>
                <RowDefinition Height="Auto"/>
            </Grid.RowDefinitions>

            <!-- Header -->
            <DockPanel LastChildFill="False">
                <TextBlock Text="PULSE // LINK" Foreground="White" FontWeight="Bold" FontFamily="Consolas" VerticalAlignment="Center"/>
                <CheckBox Content="GHOST MODE" Foreground="Gray" FontSize="10" 
                          VerticalAlignment="Center" DockPanel.Dock="Right"
                          Checked="ToggleClickThrough" Unchecked="ToggleClickThrough"
                          ToolTip="Enable mouse click-through for gaming"/>
            </DockPanel>

            <!-- Main Content -->
            <StackPanel Grid.Row="1" VerticalAlignment="Center">
                <!-- BPM Display -->
                <TextBlock Text="{Binding Bpm}" 
                           Foreground="{StaticResource NeonGreen}" 
                           FontSize="70" 
                           FontWeight="Bold" 
                           HorizontalAlignment="Center"
                           FontFamily="Arial">
                    <TextBlock.Effect>
                        <DropShadowEffect Color="#00FF41" BlurRadius="20" ShadowDepth="0"/>
                    </TextBlock.Effect>
                </TextBlock>
                
                <TextBlock Text="BPM" Foreground="White" HorizontalAlignment="Center" FontSize="12" Opacity="0.5" Margin="0,-10,0,10"/>
                
                <TextBlock Text="{Binding Status}" Foreground="Gray" HorizontalAlignment="Center" FontSize="10" FontFamily="Consolas" Margin="0,0,0,5"/>

                <!-- Device List -->
                <ListBox ItemsSource="{Binding Devices}" 
                         Height="70" 
                         Background="Transparent" 
                         BorderThickness="0" 
                         ScrollViewer.HorizontalScrollBarVisibility="Disabled">
                    <ListBox.ItemTemplate>
                        <DataTemplate>
                            <Button Content="{Binding Name}" 
                                    Command="{Binding DataContext.ConnectCommand, RelativeSource={RelativeSource AncestorType=Window}}" 
                                    CommandParameter="{Binding Id}" 
                                    FontSize="10" 
                                    Width="180"
                                    Margin="0,2"/>
                        </DataTemplate>
                    </ListBox.ItemTemplate>
                </ListBox>
            </StackPanel>

            <!-- Footer Controls -->
            <StackPanel Grid.Row="2">
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition/>
                        <ColumnDefinition/>
                    </Grid.ColumnDefinitions>
                    <Button Content="SCAN" Command="{Binding ScanCommand}" Margin="0,0,2,0"/>
                    <Button Grid.Column="1" Content="LIVE" Command="{Binding ToggleStreamCommand}" Margin="2,0,0,0"/>
                </Grid>
                
                <TextBox Text="{Binding StreamUrl}" 
                         Background="Transparent" 
                         Foreground="#555" 
                         BorderThickness="0" 
                         FontSize="9" 
                         Margin="0,5,0,0" 
                         IsReadOnly="True"
                         FontFamily="Consolas"/>
            </StackPanel>
        </Grid>
    </Border>
</Window>
```

### 8. Main Window Logic (`PulseLink/MainWindow.xaml.cs`)

```csharp
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using PulseLink.ViewModels;

namespace PulseLink;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }

    // Allow dragging the borderless window
    private void Window_MouseDown(object sender, MouseButtonEventArgs e) 
    { 
        if (e.ChangedButton == MouseButton.Left) 
            DragMove(); 
    }

    // --- Mouse Passthrough Logic (Win32 API) ---
    private void ToggleClickThrough(object sender, RoutedEventArgs e)
    {
        var checkBox = sender as CheckBox;
        if (checkBox == null) return;

        var hwnd = new WindowInteropHelper(this).Handle;
        int extendedStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        
        if (checkBox.IsChecked == true)
        {
            // Add Transparent flag (Click-through)
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle | WS_EX_TRANSPARENT);
            // Ensure Topmost
            this.Topmost = true;
        }
        else
        {
            // Remove Transparent flag
            SetWindowLong(hwnd, GWL_EXSTYLE, extendedStyle & ~WS_EX_TRANSPARENT);
        }
    }

    // Win32 Constants
    const int GWL_EXSTYLE = -20;
    const int WS_EX_TRANSPARENT = 0x00000020;

    [DllImport("user32.dll")]
    static extern int GetWindowLong(IntPtr hwnd, int index);

    [DllImport("user32.dll")]
    static extern int SetWindowLong(IntPtr hwnd, int index, int newStyle);
}
```

### 9. Application Entry / DI Config (`PulseLink/App.xaml.cs`)

```csharp
using Microsoft.Extensions.DependencyInjection;
using PulseLink.Services;
using PulseLink.ViewModels;
using System;
using System.Windows;

namespace PulseLink;

public partial class App : Application
{
    private IServiceProvider? _serviceProvider;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        var serviceCollection = new ServiceCollection();

        // 1. Register Services
        serviceCollection.AddSingleton<IBluetoothService, BluetoothService>();
        serviceCollection.AddSingleton<StreamService>();

        // 2. Register ViewModels
        serviceCollection.AddSingleton<MainViewModel>();

        // 3. Register Views
        serviceCollection.AddSingleton<MainWindow>();

        _serviceProvider = serviceCollection.BuildServiceProvider();

        // 4. Launch Main Window
        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }
}
```

### 10. Web Viewer (`PulseLink/web/index.html`)

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <title>PulseLink Live</title>
    <script src="https://cdnjs.cloudflare.com/ajax/libs/paho-mqtt/1.0.1/mqttws31.min.js"></script>
    <script src="https://cdn.jsdelivr.net/npm/chart.js"></script>
    <style>
        body { background: #0d0d0d; color: #00FF41; font-family: 'Courier New', monospace; display: flex; flex-direction: column; align-items: center; justify-content: center; height: 100vh; margin: 0; }
        .container { border: 1px solid #00FF41; padding: 20px; border-radius: 10px; width: 300px; text-align: center; box-shadow: 0 0 15px rgba(0,255,65,0.2); }
        #bpm { font-size: 80px; font-weight: bold; text-shadow: 0 0 20px #00FF41; margin: 10px 0; }
        .status { font-size: 12px; color: #555; margin-top: 10px; }
        .pump { animation: beat 0.5s infinite; }
        @keyframes beat { 0% { transform: scale(1); } 15% { transform: scale(1.1); } 30% { transform: scale(1); } }
        canvas { margin-top: 20px; max-height: 100px; }
    </style>
</head>
<body>
    <div class="container">
        <div>LIVE HEART RATE</div>
        <div id="bpm">--</div>
        <div>BPM</div>
        <canvas id="chart"></canvas>
        <div id="status" class="status">CONNECTING...</div>
    </div>
    <script>
        const urlParams = new URLSearchParams(window.location.search);
        const userId = urlParams.get('id');
        const ctx = document.getElementById('chart').getContext('2d');
        const chart = new Chart(ctx, {
            type: 'line',
            data: { labels: Array(20).fill(''), datasets: [{ data: Array(20).fill(null), borderColor: '#00FF41', borderWidth: 1, pointRadius: 0, tension: 0.4 }] },
            options: { plugins: { legend: { display: false } }, scales: { x: { display: false }, y: { min: 40, max: 180, grid: { color: '#333' } } }, animation: false }
        });

        if(userId) {
            const client = new Paho.MQTT.Client("broker.emqx.io", 8083, "Viewer_" + Math.random());
            client.onConnectionLost = (o) => document.getElementById('status').innerText = "DISCONNECTED";
            client.onMessageArrived = (msg) => {
                const d = JSON.parse(msg.payloadString);
                const el = document.getElementById('bpm');
                el.innerText = d.bpm;
                el.style.animationDuration = (60/d.bpm) + "s";
                el.classList.remove('pump'); void el.offsetWidth; el.classList.add('pump');
                
                chart.data.datasets[0].data.push(d.bpm);
                chart.data.datasets[0].data.shift();
                chart.update();
                document.getElementById('status').innerText = "LIVE SIGNAL ACTIVE";
            };
            client.connect({ onSuccess: () => { client.subscribe(`pulselink/${userId}/data`); document.getElementById('status').innerText = "WAITING FOR DATA..."; }, useSSL: true });
        } else { document.getElementById('status').innerText = "ERROR: NO USER ID"; }
    </script>
</body>
</html>
```

---

## Section 3: Build Verification

Run the following command in the terminal to ensure everything is correct:

```powershell
dotnet build PulseLink
```

If the build succeeds, the executable will be located in `PulseLink/bin/Debug/net8.0-windows/`.

*** End of Manifest ***