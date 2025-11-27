using PulseLink.Resources;
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
        StatusChanged?.Invoke(Strings.Status_Scanning);
    }

    public async Task ConnectAsync(string deviceId)
    {
        if (_watcher != null)
        {
            _watcher.Stop();
        }
        
        StatusChanged?.Invoke(Strings.Status_Connecting);
        
        try
        {
            var device = await BluetoothLEDevice.FromIdAsync(deviceId);
            if (device == null) 
            {
                StatusChanged?.Invoke(Strings.Status_Error_DeviceNotFound);
                return;
            }

            // Get Heart Rate Service (UUID 0x180D)
            var services = await device.GetGattServicesForUuidAsync(GattServiceUuids.HeartRate);
            if (services.Services.Count > 0 && services.Status == GattCommunicationStatus.Success)
            {
                // Get Measurement Characteristic (UUID 0x2A37)
                var charResult = await services.Services[0].GetCharacteristicsForUuidAsync(GattCharacteristicUuids.HeartRateMeasurement);
                if (charResult.Characteristics.Count > 0 && charResult.Status == GattCommunicationStatus.Success)
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
                    StatusChanged?.Invoke(Strings.Status_Connected);
                }
                else
                {
                    StatusChanged?.Invoke(Strings.Status_Error_NoHrCharacteristic);
                }
            }
            else 
            {
                StatusChanged?.Invoke(Strings.Status_Error_PleaseEnableHrBroadcast);
            }
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(string.Format(Strings.Status_Error_Exception, ex.Message));
        }
    }
}
