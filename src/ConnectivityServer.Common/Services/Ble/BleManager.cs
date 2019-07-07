﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConnectivityServer.Common.Models;
using ConnectivityServer.Common.Services.Notifications;
using EasyCaching.Core;

namespace ConnectivityServer.Common.Services.Ble
{
    public partial class BleManager : IBleManager
    {
        #region Fields
        private const string DiscoveredDeviceChachePrefix = "discovereddevice-";
        private object lockObject = new object();

        private readonly INotifier _onDeviceValueChangedNotifier;
        private readonly IEasyCachingProvider _cachingProvider;

        private static readonly TimeSpan DiscoveredDeviceCachingTime = TimeSpan.FromMilliseconds(5000);

        protected readonly IDictionary<string, ProxiedBleDevice> Devices = new Dictionary<string, ProxiedBleDevice>();
        #endregion

        #region ctor

        public BleManager(IEnumerable<IBleAdapter> bleAdapters, INotifier onDeviceValueChangedNotifier, IEasyCachingProvider cachingProvider)
        {
            _cachingProvider = cachingProvider;
            _onDeviceValueChangedNotifier = onDeviceValueChangedNotifier;

            foreach (var adapter in bleAdapters)
            {
                adapter.DeviceConnected += DeviceConnectedHandler;
                adapter.DeviceDiscovered += DeviceDiscoveredHandler;
                adapter.DeviceDisconnected += DeviceDisconnectedHandler;
                adapter.DeviceValueChanged += DeviceValueChangedHandler;
            }
        }
        private void DeviceConnectedHandler(IBleAdapter sender, BleDeviceEventArgs args)
        {
            var device = args.Device;
            var deviceId = device.Id;
            lock (lockObject)
            {
                if (!Devices.ContainsKey(deviceId))
                    Devices[deviceId] = new ProxiedBleDevice(sender, device);
            }
        }
        private void DeviceDiscoveredHandler(IBleAdapter sender, BleDeviceEventArgs args)
        {
            var device = args.Device;
            var deviceId = device.Id;
            var pd = new ProxiedBleDevice(sender, device);
            _cachingProvider.Set(DiscoveredDeviceChachePrefix + deviceId, pd, DiscoveredDeviceCachingTime);
        }
        private void DeviceDisconnectedHandler(IBleAdapter sender, BleDeviceEventArgs args)
        {
            var device = args.Device;
            var deviceId = device.Id;
            lock (lockObject)
            {
                Devices.Remove(deviceId);
                _cachingProvider.RemoveByPrefix(DiscoveredDeviceChachePrefix);
            }
        }
        private void DeviceValueChangedHandler(IBleAdapter sender, BleDeviceValueChangedEventArgs args)
        {
            Task.Run(() => _onDeviceValueChangedNotifier.Push(args.DeviceUuid, args));
        }

        #endregion

        public virtual IEnumerable<BleDevice> GetDiscoveredDevices()
        {
            return _cachingProvider.GetByPrefix< ProxiedBleDevice>(DiscoveredDeviceChachePrefix).Select(v => v.Value.Value.Device);
        }

        public async Task<IEnumerable<BleGattService>> GetDeviceGattServices(string deviceId)
        {
            var bleAdapter = Devices[deviceId].Adapter;
            return await bleAdapter.GetGattServices(deviceId) ?? new BleGattService[] { };
        }

        public async Task<IEnumerable<BleGattCharacteristic>> GetDeviceCharacteristics(string deviceUuid, string serviceUuid)
        {
            var gattService = await this.GetGattServiceById(deviceUuid, serviceUuid);
            return gattService.Characteristics;
        }

        public async Task<bool> Unpair(string deviceUuid)
        {
            var res = await Devices[deviceUuid].Adapter.Unpair(deviceUuid);
            if (res)
                Devices.Remove(deviceUuid);
            return res;
        }

        public async Task<bool> WriteToCharacteristric(string deviceUuid, string serviceUuid, string characteristicUuid, IEnumerable<byte> buffer)
        {
            var bleAdapter = Devices[deviceUuid].Adapter;
            return await bleAdapter.WriteToCharacteristic(deviceUuid, serviceUuid, characteristicUuid, buffer);
        }

        public async Task<IEnumerable<byte>> ReadFromCharacteristic(string deviceUuid, string serviceUuid, string characteristicUuid)
        {
            var bleAdapter = Devices[deviceUuid].Adapter;
            return await bleAdapter.ReadFromCharacteristic(deviceUuid, serviceUuid, characteristicUuid);
        }


        public async Task<bool> RegisterToCharacteristicNotifications(string deviceUuid, string serviceUuid, string characteristicUuid)
        {
            var bleAdapter = Devices[deviceUuid].Adapter;
            return await bleAdapter.GetCharacteristicNotifications(deviceUuid, serviceUuid, characteristicUuid);
        }
    }
}
