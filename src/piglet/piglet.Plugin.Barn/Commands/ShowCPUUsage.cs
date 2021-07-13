using Piglet.Plugin.Barn.Helpers;
using Piglet.SDK.Core;
using Piglet.SDK.Interfaces;
using Piglet.SDK.Models;
using Piglet.SDK.Util;
using System;
using System.Drawing;

namespace Piglet.Plugin.Barn.Commands
{
    [CompatibleWith(DeviceModel.XL)]
    [ExpectedInteractionFormat(CommandType.ACTIVATION)]
    class ShowCPUUsage : IPigletCommand
    {
        public string Name => "Launch Application";
        public string Description => "Launches an application on the machine.";

        public void ExecuteOnAction(int keyIndex, string arguments)
        {
            throw new NotImplementedException();
        }

        public void ExecuteOnActivation(int keyIndex, string arguments)
        {
            var randomIconFromText = IconGenerator.GenerateTestImageFromText("92%", new Font("Consolas", 12), Color.Red, Color.Blue);
            var resizeImage = ImageHelpers.ResizeImage(ImageHelpers.GetImageBuffer(randomIconFromText), DeviceConstants.XLButtonSize, DeviceConstants.XLButtonSize);


        }
    }
}
