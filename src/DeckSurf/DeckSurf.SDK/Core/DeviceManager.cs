// Copyright (c) Den Delimarsky
// Den Delimarsky licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DeckSurf.SDK.Models;
using DeckSurf.SDK.Util;
using HidSharp;

namespace DeckSurf.SDK.Core
{
    public class DeviceManager
    {
        private static readonly int SupportedVid = 4057;
        private static readonly int ImageReportLength = 1024;
        private static readonly int ImageReportHeaderLength = 8;
        private static readonly int ImageReportPayloadLength = ImageReportLength - ImageReportHeaderLength;

        /// <summary>
        /// Return a list of connected devices supported by Deck.Surf.
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

        public static bool SetKey(ConnectedDevice device, int keyId, byte[] image)
        {
            var content = image ?? DeviceConstants.XLDefaultBlackButton;

            if (device != null)
            {
                var iteration = 0;
                var remainingBytes = content.Length;

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

                        // TODO: This is different for different device classes, so I will need
                        // to make sure that I adjust the header format.
                        byte[] header = new byte[] { 0x02, 0x07, (byte)keyId, finalizer, bitmaskedLength, shiftedLength, bitmaskedIteration, shiftedIteration };
                        var payload = header.Concat(new ArraySegment<byte>(content, bytesSent, sliceLength)).ToArray();
                        var padding = new byte[ImageReportLength - payload.Length];

                        var finalPayload = payload.Concat(padding).ToArray();
                        stream.Write(finalPayload);

                        remainingBytes -= sliceLength;
                        iteration++;
                    }
                }

                return true;
            }
            else
            {
                return false;
            }
        }

        public static ConnectedDevice SetupDevice(ConfigurationProfile profile)
        {
            try
            {
                var devices = GetDeviceList();
                if (devices != null &&
                    devices.Any() &&
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
            foreach (var button in buttonMap)
            {
                if (button.ButtonIndex <= device.ButtonCount - 1)
                {
                    if (File.Exists(button.ButtonImagePath))
                    {
                        byte[] imageBuffer = File.ReadAllBytes(button.ButtonImagePath);
                        imageBuffer = ImageHelpers.ResizeImage(imageBuffer, DeviceConstants.XLButtonSize, DeviceConstants.XLButtonSize);
                        SetKey(device, button.ButtonIndex, imageBuffer);
                    }
                }
            }
        }

        public static bool IsSupported(int vid, int pid)
        {
            if (vid == SupportedVid && Enum.IsDefined(typeof(DeviceModel), (byte)pid))
            {
                return true;
            }

            return false;
        }

    }
}
