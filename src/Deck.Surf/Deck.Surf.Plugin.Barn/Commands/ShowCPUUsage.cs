using Deck.Surf.Plugin.Barn.Helpers;
using Deck.Surf.SDK.Core;
using Deck.Surf.SDK.Interfaces;
using Deck.Surf.SDK.Models;
using Deck.Surf.SDK.Util;
using System.Drawing;

namespace Deck.Surf.Plugin.Barn.Commands
{
    [CompatibleWith(DeviceModel.XL)]
    class ShowCPUUsage : IDSCommand
    {
        public string Name => "Launch Application";
        public string Description => "Launches an application on the machine.";

        public void ExecuteOnAction(CommandMapping mappedCommand, ConnectedDevice mappedDevice)
        {
            
        }

        public void ExecuteOnActivation(CommandMapping mappedCommand, ConnectedDevice mappedDevice)
        {
            var randomIconFromText = IconGenerator.GenerateTestImageFromText("92%", new Font("Consolas", 12), Color.Red, Color.Blue);
            var resizeImage = ImageHelpers.ResizeImage(ImageHelpers.GetImageBuffer(randomIconFromText), DeviceConstants.XLButtonSize, DeviceConstants.XLButtonSize);

            DeviceManager.SetKey(mappedDevice, mappedCommand.ButtonIndex, resizeImage);
        }
    }
}
