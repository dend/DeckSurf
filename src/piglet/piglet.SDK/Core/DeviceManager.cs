using HidSharp;
using piglet.SDK.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace piglet.SDK.Core
{
    public class DeviceManager
    {
        private static int SUPPORTED_VID = 4057;

        private const int IMAGE_REPORT_LENGTH = 1024;
        private const int IMAGE_REPORT_HEADER_LENGTH = 8;
        private const int IMAGE_REPORT_PAYLOAD_LENGTH = IMAGE_REPORT_LENGTH - IMAGE_REPORT_HEADER_LENGTH;

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
                    connectedDevices.Add(new ConnectedDevice(device.VendorID, device.ProductID, device.DevicePath, device.GetFriendlyName()));
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
            if (vid == SUPPORTED_VID && Enum.IsDefined(typeof(DeviceModel), (byte)pid))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Sets the visual appearance of a key to a pre-defined image.
        /// </summary>
        /// <param name="keyId">Numeric index for the key.</param>
        /// <param name="image">Byte array containing the JPEG image.</param>
        /// <param name="vid">VID for the target device.</param>
        /// <param name="pid">PID for the target device.</param>
        /// <returns>True if the change was successful. False if the change failed.</returns>
        public static bool SetKey(byte keyId, byte[] image, int vid, int pid)
        {
            var device = DeviceList.Local.GetHidDeviceOrNull(vid, pid);

            if (device != null)
            {
                var iteration = 0;
                var remainingBytes = image.Length;

                using (var stream = device.Open())
                {
                    while (remainingBytes > 0)
                    {
                        var sliceLength = Math.Min(remainingBytes, IMAGE_REPORT_PAYLOAD_LENGTH);
                        var bytesSent = iteration * IMAGE_REPORT_PAYLOAD_LENGTH;

                        byte finalizer = sliceLength == remainingBytes ? (byte)1 : (byte)0;
                        var bitmaskedLength = (byte)(sliceLength & 0xFF);
                        var shiftedLength = (byte)(sliceLength >> IMAGE_REPORT_HEADER_LENGTH);
                        var bitmaskedIteration = (byte)(iteration & 0xFF);
                        var shiftedIteration = (byte)(iteration >> IMAGE_REPORT_HEADER_LENGTH);

                        byte[] header = new byte[] { 0x02, 0x07, keyId, finalizer, bitmaskedLength, shiftedLength, bitmaskedIteration, shiftedIteration };
                        var payload = header.Concat(new ArraySegment<byte>(image, bytesSent, sliceLength)).ToArray();
                        var padding = new byte[IMAGE_REPORT_LENGTH - payload.Length];

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
    }
}
