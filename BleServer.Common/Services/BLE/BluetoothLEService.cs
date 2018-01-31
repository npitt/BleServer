﻿using System.Collections.Generic;
using System.Threading.Tasks;
using BleServer.Common.Domain;

namespace BleServer.Common.Services.BLE
{
    public class BluetoothLEService : IBluetoothLEService
    {
        private readonly IBleAdapter _bleAdapter;

        #region ctor
        public BluetoothLEService(IBleAdapter bleAdapter)
        {
            _bleAdapter = bleAdapter;
        }

#endregion 
        public async Task<IEnumerable<BluetoothLEDevice>> GetDevices()
        {
            return await Task.FromResult(_bleAdapter.GetDiscoveredDevices() ?? new BluetoothLEDevice[]{});
        }

        public Task<BluetoothLEDevice> GetDeviceById(string deviceId)
        {
            throw new System.NotImplementedException();
        }
    }
}