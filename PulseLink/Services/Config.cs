namespace PulseLink.Services;

public static class Config
{
    // HTTP Server for IPv6 Direct Access
    public const int HttpServerPort = 8998;
    public const string HttpServerBaseUrl = "http://[::]:8999/"; // Direct string literal

    // MQTT Broker for Web Sharing (GitHub Pages)
    public const string MqttBrokerHost = "broker.emqx.io";
    public const int MqttBrokerWebSocketPort = 8084;
    public const string MqttBrokerWebSocketUri = "wss://broker.emqx.io:8084/mqtt"; // Direct string literal
    public const string MqttGithubPagesBaseUrl = "https://sun-angel2.github.io/HeartRater/";
}
