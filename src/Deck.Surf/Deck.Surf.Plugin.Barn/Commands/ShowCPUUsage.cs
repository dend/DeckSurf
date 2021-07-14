using Deck.Surf.Plugin.Barn.Helpers;
using Deck.Surf.SDK.Core;
using Deck.Surf.SDK.Interfaces;
using Deck.Surf.SDK.Models;
using Deck.Surf.SDK.Util;
using System;
using System.Drawing;

namespace Deck.Surf.Plugin.Barn.Commands
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
