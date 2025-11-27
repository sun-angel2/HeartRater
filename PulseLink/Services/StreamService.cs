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

    public string StreamUrl => $"https://sun-angel2.github.io/HeartRater/?id={_userId}";

    public async Task StartAsync()
    {
        if (_client.IsConnected) return;

        var options = new MqttClientOptionsBuilder()
            .WithWebSocketServer("wss://broker.emqx.io:8084/mqtt")
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