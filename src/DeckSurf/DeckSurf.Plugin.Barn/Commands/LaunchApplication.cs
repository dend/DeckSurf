using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using DeckSurf.SDK.Util;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

namespace DeckSurf.Plugin.Barn.Commands
{
    [CompatibleWith(DeviceModel.XL)]
    [CompatibleWith(DeviceModel.XL2022)]
    [CompatibleWith(DeviceModel.Original)]
    [CompatibleWith(DeviceModel.Original2019)]
    [CompatibleWith(DeviceModel.MK2)]
    [CompatibleWith(DeviceModel.Mini)]
    [CompatibleWith(DeviceModel.Mini2022)]
    [CompatibleWith(DeviceModel.Plus)]
    [CompatibleWith(DeviceModel.Neo)]
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
            if (!string.IsNullOrEmpty(mappedCommand.ButtonImagePath))
            {
                return;
            }

            try
            {
                byte[] imageBytes = null;

                if (OperatingSystem.IsWindows())
                {
                    imageBytes = TryGetWindowsFileIcon(mappedCommand.CommandArguments, mappedDevice);
                }

                if (imageBytes == null)
                {
                    // Cross-platform fallback: use a custom image if the command
                    // argument points to an image file, otherwise set a colored key.
                    var arg = mappedCommand.CommandArguments;
                    if (File.Exists(arg) && IsImageFile(arg))
                    {
                        imageBytes = File.ReadAllBytes(arg);
                    }
                }

                if (imageBytes != null)
                {
                    var resized = ImageHelper.ResizeImage(
                        imageBytes,
                        mappedDevice.ButtonResolution,
                        mappedDevice.ButtonResolution,
                        mappedDevice.ImageRotation,
                        mappedDevice.KeyImageFormat);
                    mappedDevice.SetKey(mappedCommand.ButtonIndex, resized);
                }
                else
                {
                    // No icon available — set a recognizable colored key.
                    mappedDevice.SetKeyColor(mappedCommand.ButtonIndex, DeviceColor.Cyan);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not set icon for {mappedCommand.CommandArguments}: {ex.Message}");
                mappedDevice.SetKeyColor(mappedCommand.ButtonIndex, DeviceColor.Cyan);
            }
        }

        [SupportedOSPlatform("windows")]
        private static byte[] TryGetWindowsFileIcon(string filePath, IConnectedDevice device)
        {
            try
            {
                using var bitmap = ImageHelper.GetFileIcon(
                    filePath,
                    device.ButtonResolution,
                    device.ButtonResolution,
                    SIIGBF.SIIGBF_ICONONLY | SIIGBF.SIIGBF_CROPTOSQUARE);

                using var ms = new MemoryStream();
                bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                return ms.ToArray();
            }
            catch
            {
                return null;
            }
        }

        private static bool IsImageFile(string path)
        {
            var ext = Path.GetExtension(path);
            return ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".gif", StringComparison.OrdinalIgnoreCase);
        }

        public void Dispose()
        {
        }
    }
}
