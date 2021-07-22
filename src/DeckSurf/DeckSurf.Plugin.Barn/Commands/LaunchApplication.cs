using DeckSurf.SDK.Core;
using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using DeckSurf.SDK.Util;
using System.Diagnostics;

namespace DeckSurf.Plugin.Barn.Commands
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
                    var icon = ImageHelpers.GetFileIcon(mappedCommand.CommandArguments, DeviceConstants.XLButtonSize, DeviceConstants.XLButtonSize, SIIGBF.SIIGBF_ICONONLY | SIIGBF.SIIGBF_CROPTOSQUARE);
                    var byteContent = ImageHelpers.GetImageBuffer(icon);
                    DeviceManager.SetKey(mappedDevice, mappedCommand.ButtonIndex, byteContent);
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
