using Deck.Surf.SDK.Core;
using Deck.Surf.SDK.Interfaces;
using Deck.Surf.SDK.Models;
using Deck.Surf.SDK.Util;
using System.Diagnostics;

namespace Deck.Surf.Plugin.Barn.Commands
{
    [CompatibleWith(DeviceModel.XL)]
    class LaunchApplication : IDSCommand
    {
        public string Name => "Launch Application";
        public string Description => "Launches an application on the machine.";

        public void ExecuteOnAction(CommandMapping mappedCommand, ConnectedDevice mappedDevice, int activatingButton = -1)
        {
            Process.Start(mappedCommand.CommandArguments);
        }

        public void ExecuteOnActivation(CommandMapping mappedCommand, ConnectedDevice mappedDevice)
        {
            if (string.IsNullOrEmpty(mappedCommand.ButtonImagePath))
            {
                try
                {
                    var icon = ImageHelpers.GetFileIcon(mappedCommand.CommandArguments, 256, 256, SIIGBF.SIIGBF_ICONONLY);
                    var byteContent = ImageHelpers.GetImageBuffer(icon);
                    var targetImage = ImageHelpers.ResizeImage(byteContent, DeviceConstants.XLButtonSize, DeviceConstants.XLButtonSize);
                    DeviceManager.SetKey(mappedDevice, mappedCommand.ButtonIndex, targetImage);
                }
                catch
                {
                    // Could not set up the right configuration for the button image.
                    // Should add some logging here for the plugin.
                    Debug.WriteLine($"Could not set icon for {mappedCommand.CommandArguments}");
                }
            }
        }
    }
}
