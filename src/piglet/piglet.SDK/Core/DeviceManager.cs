// Copyright (c) Den Delimarsky
// Den Delimarsky licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using HidSharp;
using Piglet.SDK.Models;

namespace Piglet.SDK.Core
{
    public class DeviceManager
    {
        private static readonly int SupportedVid = 4057;
        private static readonly int ImageReportLength = 1024;
        private static readonly int ImageReportHeaderLength = 8;
        private static readonly int ImageReportPayloadLength = ImageReportLength - ImageReportHeaderLength;

        /// <summary>
        /// Return a list of connected devices supported by Piglet.
        /// </summary>
        /// <returns>Enumerable containing a list of supported devices.</returns>
        public static IEnumerable<ConnectedDevice> GetDeviceList()
        {
            var connectedDevices = new List<ConnectedDevice>();
            var deviceList = DeviceList.Local.GetHidDevices();

            foreach (var device in deviceList)
            {
                bool supported = IsSupported(device.VendorID, device.ProductID);
                if (supported)
                {
                    connectedDevices.Add(new ConnectedDevice(device.VendorID, device.ProductID, device.DevicePath, device.GetFriendlyName(), (DeviceModel)device.ProductID));
                }
            }

            return connectedDevices;
        }

        /// <summary>
        /// Determines 
        /// </summary>
        /// <param name="vid"></param>
        /// <param name="pid"></param>
        /// <returns></returns>
        private static bool IsSupported(int vid, int pid)
        {
            if (vid == SupportedVid && Enum.IsDefined(typeof(DeviceModel), (byte)pid))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Sets the visual appearance of a key to a pre-defined image.
        /// </summary>
        /// <param name="deviceIndex">Zero-based index of the device that is currently in operation.</param>
        /// <param name="keyId">Numeric index for the key.</param>
        /// <param name="image">Byte array containing the JPEG image.</param>
        /// <param name="vid">VID for the target device.</param>
        /// <param name="pid">PID for the target device.</param>
        /// <returns>True if the change was successful. False if the change failed.</returns>
        public static bool SetKey(int deviceIndex, byte keyId, byte[] image, int vid, int pid)
        {
            var devices = DeviceList.Local.GetHidDevices(vid, pid);

            if (deviceIndex <= (devices.Count() - 1))
            {
                var device = devices.ElementAt(deviceIndex);
                if (device != null)
                {
                    var iteration = 0;
                    var remainingBytes = image.Length;

                    using (var stream = device.Open())
                    {
                        while (remainingBytes > 0)
                        {
                            var sliceLength = Math.Min(remainingBytes, ImageReportPayloadLength);
                            var bytesSent = iteration * ImageReportPayloadLength;

                            byte finalizer = sliceLength == remainingBytes ? (byte)1 : (byte)0;
                            var bitmaskedLength = (byte)(sliceLength & 0xFF);
                            var shiftedLength = (byte)(sliceLength >> ImageReportHeaderLength);
                            var bitmaskedIteration = (byte)(iteration & 0xFF);
                            var shiftedIteration = (byte)(iteration >> ImageReportHeaderLength);

                            byte[] header = new byte[] { 0x02, 0x07, keyId, finalizer, bitmaskedLength, shiftedLength, bitmaskedIteration, shiftedIteration };
                            var payload = header.Concat(new ArraySegment<byte>(image, bytesSent, sliceLength)).ToArray();
                            var padding = new byte[ImageReportLength - payload.Length];

                            var finalPayload = payload.Concat(padding).ToArray();
                            stream.Write(finalPayload);

                            remainingBytes -= sliceLength;
                            iteration++;
                        }
                    }

                    return true;
                }

                return false;
            }
            else
            {
                throw new IndexOutOfRangeException("Device index is not within the range of the number of connected devices.");
            }
        }

        public static ConnectedDevice SetupDevice(ConfigurationProfile profile)
        {
            try
            {
                var devices = GetDeviceList();
                if (devices != null &&
                    devices.Count() != 0 &&
                    profile.DeviceIndex <= devices.Count() - 1)
                {
                    var targetDevice = devices.ElementAt(profile.DeviceIndex);
                    SetupDeviceButtonMap(targetDevice, profile.ButtonMap);
                    return targetDevice;
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
        }

        public static void SetupDeviceButtonMap(ConnectedDevice device, IEnumerable<CommandMapping> buttonMap)
        {

        }
    }
}
