using MQTTnet;
using MQTTnet.Client;
using System;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;

namespace PulseLink.Services;

public class StreamService : IDisposable, INotifyPropertyChanged
{
    private readonly IMqttClient _client;
    private string _userId;

    public event PropertyChangedEventHandler? PropertyChanged;

    public StreamService()
    {
        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();
        _userId = Guid.NewGuid().ToString("N").Substring(0, 8); // Initialize directly

        // Notify that UserId has been set
        OnPropertyChanged(nameof(UserId));
        OnPropertyChanged(nameof(StreamUrl)); 
    }

    public string UserId
    {
        get => _userId;
        // The setter is removed as _userId is initialized once in the constructor
    }

    public string StreamUrl => Config.MqttGithubPagesBaseUrl + "?id=" + UserId!;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public async Task StartAsync()
    {
        if (_client.IsConnected) return;

        var options = new MqttClientOptionsBuilder()
            .WithWebSocketServer(o => o.WithUri(Config.MqttBrokerWebSocketUri))
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

    public void Dispose()
    {
        // Gracefully disconnect
        if (_client.IsConnected)
        {
            // Try to publish offline status, but don't wait long
            var options = new MqttApplicationMessageBuilder()
                .WithTopic($"pulselink/{_userId}/status")
                .WithPayload("offline")
                .WithRetainFlag()
                .Build();
            _client.PublishAsync(options).Wait(TimeSpan.FromSeconds(1));
        }

        // Disconnect and dispose
        _client.DisconnectAsync().Wait(TimeSpan.FromSeconds(1));
        _client.Dispose();
    }
}