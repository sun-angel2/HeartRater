using System;
using System.Threading.Tasks;

namespace PulseLink.Services;

public interface IBluetoothService : IDisposable
{
    event Action<int> HeartRateUpdated;
    event Action<string> StatusChanged;
    void StartScan();
    Task ConnectAsync(string deviceId);
    Task DisconnectAsync();
}