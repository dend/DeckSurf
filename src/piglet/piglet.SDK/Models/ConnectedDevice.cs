// Copyright (c) Den Delimarsky
// Den Delimarsky licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using HidSharp;
using Piglet.SDK.Core;

namespace Piglet.SDK.Models
{
    public class ConnectedDevice
    {
        private const int ButtonPressHeaderOffset = 4;

        private byte[] keyPressBuffer = new byte[1024];

        public ConnectedDevice()
        {
        }

        public ConnectedDevice(int vid, int pid, string path, string name, DeviceModel model)
        {
            this.VID = vid;
            this.PID = pid;
            this.Path = path;
            this.Name = name;
            this.Model = model;

            this.ButtonCount = model switch
            {
                DeviceModel.XL => DeviceConstants.XLButtonCount,
                DeviceModel.ORIGINAL => DeviceConstants.OriginalButtonCount,
                DeviceModel.MINI => DeviceConstants.MiniButtonCount,
                DeviceModel.ORIGINAL_V2 => DeviceConstants.OriginalV2ButtonCount,
                _ => 0,
            };
        }

        public delegate void ReceivedButtonPressHandler(object source, ButtonPressEventArgs e);

        public event ReceivedButtonPressHandler OnButtonPress;

        public int VID { get; set; }

        public int PID { get; set; }

        public string Path { get; set; }

        public string Name { get; set; }

        public DeviceModel Model { get; set; }

        public int ButtonCount { get; }

        private Device UnderlyingDevice { get; set; }

        private DeviceStream UnderlyingInputStream { get; set; }

        public void InitializeDevice()
        {
            this.UnderlyingDevice = DeviceList.Local.GetHidDeviceOrNull(VID, PID);

            this.UnderlyingInputStream = this.UnderlyingDevice.Open();
            this.UnderlyingInputStream.ReadTimeout = Timeout.Infinite;
            this.UnderlyingInputStream.BeginRead(this.keyPressBuffer, 0, this.keyPressBuffer.Length, this.KeyPressCallback, null);
        }

        private void KeyPressCallback(IAsyncResult result)
        {
            int bytesRead = this.UnderlyingInputStream.EndRead(result);

            // TODO: Make sure that I am checking what device type is introduced here, because not every device is a StreamDeck XL.
            var buttonData = new ArraySegment<byte>(this.keyPressBuffer, ButtonPressHeaderOffset, DeviceConstants.XLButtonCount).ToArray();
            var pressedButton = Array.IndexOf(buttonData, (byte)1);
            var buttonKind = pressedButton != -1 ? ButtonEventKind.DOWN : ButtonEventKind.UP;

            if (this.OnButtonPress != null)
            {
                this.OnButtonPress(this.UnderlyingDevice, new ButtonPressEventArgs(pressedButton, buttonKind));
            }

            Array.Clear(this.keyPressBuffer, 0, this.keyPressBuffer.Length);

            this.UnderlyingInputStream.BeginRead(this.keyPressBuffer, 0, this.keyPressBuffer.Length, this.KeyPressCallback, null);
        }
    }
}
