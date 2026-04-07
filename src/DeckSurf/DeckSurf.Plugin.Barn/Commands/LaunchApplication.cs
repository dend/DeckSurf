using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using DeckSurf.SDK.Util;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace DeckSurf.Plugin.Barn.Commands
{
    [CompatibleWith(DeviceModel.XL)]
    class LaunchApplication : IDeckSurfCommand
    {
        public string Name => "Launch Application";
        public string Description => "Launches an application on the machine.";

        public void ExecuteOnAction(CommandMapping mappedCommand, IConnectedDevice mappedDevice, int activatingButton = -1)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = mappedCommand.CommandArguments,
                UseShellExecute = true,
            });
        }

        public void ExecuteOnActivation(CommandMapping mappedCommand, IConnectedDevice mappedDevice)
        {
            if (string.IsNullOrEmpty(mappedCommand.ButtonImagePath))
            {
                try
                {
                    using var bitmap = ImageHelper.GetFileIcon(mappedCommand.CommandArguments, mappedDevice.ButtonResolution, mappedDevice.ButtonResolution, SIIGBF.SIIGBF_ICONONLY | SIIGBF.SIIGBF_CROPTOSQUARE);

                    byte[] byteContent;
                    using (var ms = new MemoryStream())
                    {
                        bitmap.Save(ms, ImageFormat.Png);
                        byteContent = ms.ToArray();
                    }

                    var resizedByteContent = ImageHelper.ResizeImage(byteContent, mappedDevice.ButtonResolution, mappedDevice.ButtonResolution, mappedDevice.ImageRotation, mappedDevice.KeyImageFormat);
                    mappedDevice.SetKey(mappedCommand.ButtonIndex, resizedByteContent);
                }
                catch
                {
                    // Could not set up the right configuration for the button image.
                    Debug.WriteLine($"Could not set icon for {mappedCommand.CommandArguments}");
                }
            }
        }

        public void Dispose()
        {
        }
    }
}
