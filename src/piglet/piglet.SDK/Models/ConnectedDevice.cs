using HidSharp;
using piglet.SDK.Util;
using System;
using System.Threading;

namespace piglet.SDK.Models
{
    public class ConnectedDevice
    {
        const int BUTTON_PRESS_HEADER_OFFSET = 4;
        const int BUTTON_NUMBER_XL = 32;

        private Device UnderlyingDevice { get; set; }
        private DeviceStream UnderlyingInputStream { get; set; }

        public int VID { get; set; }
        public int PID { get; set; }
        public string Path { get; set; }
        public string Name { get; set; }
        public DeviceModel Model { get; set; }

        public delegate void ReceivedButtonPressHandler(object source, ButtonPressEventArgs e);
        public event ReceivedButtonPressHandler OnButtonPress;

        private byte[] _keyPressBuffer = new byte[1024];

        public ConnectedDevice()
        {
        }

        public ConnectedDevice(int vid, int pid, string path, string name, DeviceModel model)
        {
            VID = vid;
            PID = pid;
            Path = path;
            Name = name;
            Model = model;
        }

        public void InitializeDevice()
        {
            UnderlyingDevice = DeviceList.Local.GetHidDeviceOrNull(VID, PID);

            UnderlyingInputStream = UnderlyingDevice.Open();
            UnderlyingInputStream.ReadTimeout = Timeout.Infinite;
            UnderlyingInputStream.BeginRead(_keyPressBuffer, 0, _keyPressBuffer.Length, KeyPressCallback, null);
        }

        private void KeyPressCallback(IAsyncResult result)
        {
            int bytesRead = UnderlyingInputStream.EndRead(result);

            // TODO: Make sure that I am checking what device type is introduced here, because not every device is a StreamDeck XL.
            var buttonData = new ArraySegment<byte>(_keyPressBuffer, BUTTON_PRESS_HEADER_OFFSET, BUTTON_NUMBER_XL).ToArray();
            var pressedButton = Array.IndexOf(buttonData, (byte)1);
            var buttonKind = pressedButton != -1 ? ButtonEventKind.DOWN : ButtonEventKind.UP;

            if (OnButtonPress != null)
            {
                OnButtonPress(UnderlyingDevice, new ButtonPressEventArgs(pressedButton, buttonKind));
            }

            Array.Clear(_keyPressBuffer, 0, _keyPressBuffer.Length);

            UnderlyingInputStream.BeginRead(_keyPressBuffer, 0, _keyPressBuffer.Length, KeyPressCallback, null);
        }
    }
}
