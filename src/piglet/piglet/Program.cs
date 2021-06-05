using HidSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace piglet
{
    class Program
    {
        //const string VID_MANUFACTURER = "0fd9"; // 4057 for integer
        // PID_SD_ORIGINAL = "0060"; // 96
        // PID_SD_ORIGINAL_V2 = "006d";  // 109
        // PID_SD_MINI = "0063"; // 99
        // PID_SD_XL = "006c"; // 108
        //static string[] PID_STRINGS = new string[] { "0060", "006d", "0063", "006c" };

        static int SUPPORTED_VID = 4057;
        static int[] SUPPORTED_PIDS = new int[] { 96, 109, 99, 108 };

        const int IMAGE_REPORT_LENGTH = 1024;
        const int IMAGE_REPORT_HEADER_LENGTH = 8;
        const int IMAGE_REPORT_PAYLOAD_LENGTH = IMAGE_REPORT_LENGTH - IMAGE_REPORT_HEADER_LENGTH;

        static void Main(string[] args)
        {
            Console.WriteLine("Hello World!");
            var deviceList = DeviceList.Local.GetHidDevices();

            foreach(var device in deviceList)
            {
                bool supported = IsSupported(device.VendorID, device.ProductID);
                if (supported)
                {
                    Console.WriteLine("Found supported device!");
                    SetKey(0, @"C:\Users\Librarian\Downloads\test.jpg", device.VendorID, device.ProductID);
                }
            }

            Console.ReadKey();
        }

        /// <summary>
        /// Sets the visual appearance of a key to a pre-defined image.
        /// </summary>
        /// <param name="keyId">Numeric index for the key.</param>
        /// <param name="imagePath">Local path to the JPEG image file.</param>
        /// <param name="vid">VID for the target device.</param>
        /// <param name="pid">PID for the target device.</param>
        /// <returns>True if the change was successful. False if the change failed.</returns>
        static bool SetKey(byte keyId, string imagePath, int vid, int pid)
        {
            var device = DeviceList.Local.GetHidDeviceOrNull(vid, pid);

            if (device != null)
            {
                if (File.Exists(imagePath))
                {
                    byte[] imageData = File.ReadAllBytes(imagePath);

                    var iteration = 0;
                    var remainingBytes = imageData.Length;

                    using (var stream = device.Open())
                    {
                        while (remainingBytes > 0)
                        {
                            var sliceLength = Math.Min(remainingBytes, IMAGE_REPORT_PAYLOAD_LENGTH);
                            var bytesSent = iteration * IMAGE_REPORT_PAYLOAD_LENGTH;

                            byte finalizer = sliceLength == bytesSent ? (byte)1 : (byte)0;
                            var bitmaskedLength = (byte)(sliceLength & 0xFF);
                            var shiftedLength = (byte)(sliceLength >> IMAGE_REPORT_HEADER_LENGTH);
                            var bitmaskedIteration = (byte)(iteration & 0xFF);
                            var shiftedIteration = (byte)(iteration >> IMAGE_REPORT_HEADER_LENGTH);

                            byte[] header = new byte[] { 0x02, 0x07, keyId, finalizer, bitmaskedLength, shiftedLength, bitmaskedIteration, shiftedIteration };
                            var payload = header.Concat(new ArraySegment<byte>(imageData, bytesSent, sliceLength)).ToArray();
                            var padding = new byte[IMAGE_REPORT_LENGTH - payload.Length];

                            var finalPayload = payload.Concat(padding).ToArray();
                            stream.Write(finalPayload);

                            remainingBytes -= sliceLength;
                            iteration++;
                        }
                    }
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Parse the vendor and product IDs from the device path.
        /// </summary>
        /// <param name="id">Path to the HID device.</param>
        /// <returns>Null if there is no matching string. A key-value pair with VID and PID if a match is found.</returns>
        static KeyValuePair<string, string>? GetDeviceId(string id)
        {
            string deviceRegex = "vid_([a-zA-Z0-9]+){1}.+pid_([a-zA-Z0-9]+)";
            var matches = Regex.Matches(id, deviceRegex);

            if (matches != null && matches.Count > 0)
            {
                foreach(var match in matches)
                {
                    var vid = ((Match)match).Groups[1];
                    var pid = ((Match)match).Groups[2];

                    return new KeyValuePair<string, string>(vid.Value, pid.Value);
                }
            }
            return null;
        }

        /// <summary>
        /// Determines 
        /// </summary>
        /// <param name="vid"></param>
        /// <param name="pid"></param>
        /// <returns></returns>
        static bool IsSupported(int vid, int pid)
        {
            if (vid == SUPPORTED_VID && SUPPORTED_PIDS.Any(x => x == pid))
            {
                return true;
            }
            return false;
        }
    }
}
