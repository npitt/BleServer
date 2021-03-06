﻿using System.Net;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;
using ConnectivityServer.Common.Models;
using ConnectivityServer.Common.Services.Ble;

namespace ConnectivityServer.WebApi.Controllers
{
    [Route("api/ble/[controller]")]
    public class DeviceController : Controller
    {
        private readonly IBleService _blutoothservice;

        public DeviceController(IBleService blutoothservice)
        {
            _blutoothservice = blutoothservice;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllDiscoveredDevices()
        {
            var devices = await _blutoothservice.GetDiscoveredDevices() ?? new BleDevice[] { };
            return Ok(devices);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetDeviceById(string id)
        {
            var device = await _blutoothservice.GetDiscoveredDeviceById(id);
            return device != null
                ? Ok(device)
                : NotFound(new
                {
                    message = "Failed to find bluetooth device",
                    @id = id
                }) as IActionResult;
        }

        /// <summary>
        /// Disconnects from device
        /// </summary>
        /// <param name="id">deviceId</param>
        /// <returns></returns>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DisconnectDeviceById(string id)
        {
            var wasDisconnected = await _blutoothservice.DisconnectDeviceById(id);
            return wasDisconnected
                ? Accepted()
                : StatusCode((int)HttpStatusCode.NotAcceptable, new
                {
                    message = "Failed to disconnect device",
                    @id = id
                }) as IActionResult;
        }
    }
}
