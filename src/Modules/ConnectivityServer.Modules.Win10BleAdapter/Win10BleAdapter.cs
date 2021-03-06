﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
using ConnectivityServer.Common.Models;
using ConnectivityServer.Common.Services.Ble;
using System.Collections.Concurrent;
using Windows.Devices.Enumeration;

namespace ConnectivityServer.Modules.Win10BleAdapter
{
    public class Win10BleAdapter : IBleAdapter
    {

        #region fields

        private readonly BluetoothLEAdvertisementWatcher _bleWatcher;
        private static readonly IDictionary<string, BluetoothLEDevice> _devices = new ConcurrentDictionary<string, BluetoothLEDevice>();
        private static readonly IDictionary<string, GattCharacteristic> _characteristics = new Dictionary<string, GattCharacteristic>();
        private static readonly IDictionary<string, GattDeviceService> _services = new Dictionary<string, GattDeviceService>();

        private readonly object lockObj = new object();

        #endregion

        public async Task<bool> Disconnect(string deviceId)
        {
            var bleDevice = _devices[deviceId];
            var result = true;
            var p = bleDevice.DeviceInformation.Pairing;
            if (p.IsPaired)
            {
                var unpairingResult = await p.UnpairAsync();
                var s = unpairingResult.Status;
                result = unpairingResult.Status == DeviceUnpairingResultStatus.AlreadyUnpaired ||
                             unpairingResult.Status == DeviceUnpairingResultStatus.Unpaired;
            }

            var domainModel = bleDevice.ToDomainModel();
            result = await ClearDevice(deviceId);
            OnDeviceDisconnected(new BleDeviceEventArgs(bleDevice.ToDomainModel()));
            return result;
        }

        public async Task<IEnumerable<BleGattService>> GetGattServices(string deviceUuid)
        {
            var gattDeviceServices = await _devices[deviceUuid].GetGattServicesAsync();
            var result = new List<BleGattService>();
            foreach (var gds in gattDeviceServices.Services)
                result.Add(await ExtractDomainModel(gds));
            return result;
        }

        private async Task<GattDeviceService> GetGattServiceByUuid(string deviceUuid, string serviceUuid)
        {
            var srvKey = $"{deviceUuid}_{serviceUuid}";
            if (_services.TryGetValue(srvKey, out var service) && service.Session.SessionStatus == GattSessionStatus.Active)
                return service;

            var gattServices = await _devices[deviceUuid].GetGattServicesForUuidAsync(Guid.Parse(serviceUuid), BluetoothCacheMode.Cached);
            service = gattServices.Services.First();
            _services[srvKey] = service;

            return service;
        }

        public event BleDeviceEventHandler DeviceConnected;
        public event BleDeviceEventHandler DeviceDiscovered;
        public event BleDeviceEventHandler DeviceDisconnected;
        public event BluetoothDeviceValueChangedEventHandler DeviceValueChanged;

        public async Task<IEnumerable<byte>> ReadFromCharacteristic(string deviceUuid, string serviceUuid, string characteristicUuid)
        {
            var characteristic = await GetCharacteristicAsync(deviceUuid, serviceUuid, characteristicUuid);
            var gattReadResult = await characteristic.ReadValueAsync();

            if (gattReadResult.Status != GattCommunicationStatus.Success)
                return null;

            using (var reader = DataReader.FromBuffer(gattReadResult.Value))
            {
                var value = new byte[reader.UnconsumedBufferLength];
                reader.ReadBytes(value);
                return value;
            }
        }

        public async Task<bool> WriteToCharacteristic(string deviceUuid, string serviceUuid, string characteristicUuid,
            IEnumerable<byte> buffer)
        {
            var characteristic = await GetCharacteristicAsync(deviceUuid, serviceUuid, characteristicUuid);
            using (var writer = new DataWriter())
            {
                writer.WriteBytes(buffer.ToArray());
                var status = await characteristic.WriteValueAsync(writer.DetachBuffer(), GattWriteOption.WriteWithoutResponse);
                return status == GattCommunicationStatus.Success;
            }
        }

        private async Task<GattCharacteristic> GetCharacteristicAsync(string deviceUuid, string serviceUuid,
            string characteristicUuid)
        {
            var chKey = $"{deviceUuid}_{serviceUuid}_{characteristicUuid}";

            if (_characteristics.TryGetValue(chKey, out var characteristic))
            {
                try
                {
                    if (characteristic.Service.Session.SessionStatus == GattSessionStatus.Active)
                        return characteristic;
                }
                catch (ObjectDisposedException)
                {
                }
            }

            var service = await GetGattServiceByUuid(deviceUuid, serviceUuid);
            var allCharacteristics = await service.GetCharacteristicsForUuidAsync(Guid.Parse(characteristicUuid), BluetoothCacheMode.Cached);
            var result = allCharacteristics.Characteristics.First();
            _characteristics[chKey] = result;
            return result;
        }

        public async Task<bool> GetCharacteristicNotifications(string deviceUuid, string serviceUuid, string characteristicUuid)
        {
            var readCharacteristic = await GetCharacteristicAsync(deviceUuid, serviceUuid, characteristicUuid);

            var status =
                await readCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                    GattClientCharacteristicConfigurationDescriptorValue.Notify);
            var result = status == GattCommunicationStatus.Success;

            if (result)
                readCharacteristic.ValueChanged += ProcessAndNotify;

            return result;
        }
        private void ProcessAndNotify(GattCharacteristic sender, GattValueChangedEventArgs args)
        {
            byte[] buffer;
            using (var reader = DataReader.FromBuffer(args.CharacteristicValue))
            {
                buffer = new byte[reader.UnconsumedBufferLength];
                reader.ReadBytes(buffer);
            }

            var newValue = Encoding.UTF8.GetString(buffer, 0, buffer.Length);
            var valueChanged = new BleDeviceValueChangedEventArgs(sender.Service.Session.DeviceId.Id,
                sender.Service.Uuid.ToString(),
                sender.Uuid.ToString(),
                newValue);

            DeviceValueChanged?.Invoke(this, valueChanged);
        }

        public void Start()
        {
            _bleWatcher.Start();
        }

        private static async Task<BleGattService> ExtractDomainModel(GattDeviceService gattDeviceService)
        {
            var srvChars = await gattDeviceService.GetCharacteristicsAsync(BluetoothCacheMode.Cached);

            return new BleGattService
            {
                Uuid = gattDeviceService.Uuid,
                DeviceId = gattDeviceService.Session?.DeviceId?.Id ?? string.Empty,
                Characteristics = srvChars.Characteristics.AsEnumerable()
                    .Select(sc => new BleGattCharacteristic(sc.Uuid, sc.UserDescription)).ToArray()
            };
        }


        #region ctor

        public Win10BleAdapter()
        {
            _bleWatcher = InitBleWatcher();
        }

        private BluetoothLEAdvertisementWatcher InitBleWatcher()
        {
            var bleWatcher = new BluetoothLEAdvertisementWatcher
            {
                ScanningMode = BluetoothLEScanningMode.Active
            };
            bleWatcher.Received += async (w, btAdv) =>
            {
                var bleDevice = await ExtractBleDeviceByBluetoothAddress(btAdv.BluetoothAddress);
                if (bleDevice == null)
                    return;
                var bleDeviceId = bleDevice.DeviceId;

                lock (lockObj)
                {
                    if (!_devices.ContainsKey(bleDeviceId))
                        _devices[bleDeviceId] = bleDevice;
                }

                DeviceDiscovered?.Invoke(this, new BleDeviceEventArgs(bleDevice.ToDomainModel()));
                bleDevice.ConnectionStatusChanged += UpdateByConnectionStatusAndPublishEvent;
            };
            return bleWatcher;
        }
        private void UpdateByConnectionStatusAndPublishEvent(BluetoothLEDevice sender, object args)
        {
            if (sender.ConnectionStatus == BluetoothConnectionStatus.Connected)
            {
                if (!_devices.ContainsKey(sender.DeviceId))
                    _devices[sender.DeviceId] = sender;
                DeviceConnected?.Invoke(this, new BleDeviceEventArgs(sender.ToDomainModel()));
                return;
            }

            if (sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                if (_devices.ContainsKey(sender.DeviceId))
                {
                    var t = ClearDevice(sender.DeviceId).Result;
                }
                OnDeviceDisconnected(new BleDeviceEventArgs(sender.ToDomainModel()));
            }
        }
        private async Task<bool> ClearDevice(string deviceId)
        {
            BluetoothLEDevice curDevice;
            lock (lockObj)
            {
                if (!_devices.TryGetValue(deviceId, out curDevice))
                    return false;
                _devices.Remove(deviceId);
            }
            GattCommunicationStatus gcs = GattCommunicationStatus.Success;
            var charsToRemove = _characteristics.Where(x => x.Key.StartsWith(deviceId)).ToArray();
            for (int i = 0; i < charsToRemove.Count(); i++)
            {
                var curChar = charsToRemove.ElementAt(i);
                _characteristics.Remove(curChar);
                gcs = gcs | await curChar.Value.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.None);
            }
            var servicesToRemove = _services.Where(x => x.Key.StartsWith(deviceId)).ToArray();
            for (int i = 0; i < servicesToRemove.Count(); i++)
            {
                var curSrv = servicesToRemove.ElementAt(i);
                _services.Remove(curSrv);
                curSrv.Value.Dispose();
                gcs = gcs | GattCommunicationStatus.Success;
            }

            curDevice.Dispose();
            return gcs == GattCommunicationStatus.Success;
        }
        protected virtual void OnDeviceDisconnected(BleDeviceEventArgs args)
        {
            DeviceDisconnected?.Invoke(this, args);
        }

        private static async Task<BluetoothLEDevice> ExtractBleDeviceByBluetoothAddress(ulong bluetoothAddres)
        {
            try
            {
                return await BluetoothLEDevice.FromBluetoothAddressAsync(bluetoothAddres);
            }
            catch (Exception)
            {
                return null;
            }
        }

        #endregion
    }
}
