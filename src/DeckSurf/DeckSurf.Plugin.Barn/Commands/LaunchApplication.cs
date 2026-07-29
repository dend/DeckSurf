using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using DeckSurf.SDK.Util;
using System;
using System.Diagnostics;
using System.IO;
using System.Drawing.Imaging;
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
    [CommandParameter("path", CommandParameterType.FilePath, DisplayName = "Application path", Description = "Executable, document, or URL to open when the button is pressed.", Required = true)]
    class LaunchApplication : IDeckSurfCommand
    {
        public string Name => "Launch Application";
        public string Description => "Launches an application from a Stream Deck button.";

        public void ExecuteOnAction(CommandMapping mappedCommand, IConnectedDevice mappedDevice, int activatingButton = -1)
        {
            var target = GetTargetPath(mappedCommand);

            if (OperatingSystem.IsMacOS())
            {
                // macOS needs 'open' to launch .app bundles.
                Process.Start("open", target);
            }
            else if (OperatingSystem.IsLinux())
            {
                // Linux needs 'xdg-open' to handle desktop files and URLs.
                Process.Start("xdg-open", target);
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = target,
                    UseShellExecute = false,
                });
            }
        }

        // Profiles written before the parameter schema existed store the raw
        // path as the whole arguments string, so fall back to it when there
        // is no 'path' key.
        private static string GetTargetPath(CommandMapping mappedCommand)
        {
            return CommandArgumentParser.TryGetValue(mappedCommand.CommandArguments, "path", out var path)
                ? path
                : mappedCommand.CommandArguments;
        }

        public void ExecuteOnActivation(CommandMapping mappedCommand, IConnectedDevice mappedDevice)
        {
            if (!string.IsNullOrEmpty(mappedCommand.ButtonImagePath))
            {
                return;
            }

            var target = GetTargetPath(mappedCommand);

            try
            {
                byte[] imageBytes = null;

                if (OperatingSystem.IsWindows())
                {
                    imageBytes = TryGetWindowsFileIcon(target, mappedDevice);
                }

                if (imageBytes == null)
                {
                    // Cross-platform fallback: use a custom image if the command
                    // argument points to an image file, otherwise set a colored key.
                    if (File.Exists(target) && IsImageFile(target))
                    {
                        imageBytes = File.ReadAllBytes(target);
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
                    SetFallbackKey(mappedCommand, mappedDevice);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Could not set icon for {target}: {ex.Message}");
                SetFallbackKey(mappedCommand, mappedDevice);
            }
        }

        // SetKeyColor only works on the Stream Deck Neo, so the recognizable
        // colored fallback key is rendered as a full key image instead.
        private static void SetFallbackKey(CommandMapping mappedCommand, IConnectedDevice mappedDevice)
        {
            var cyanImage = ImageHelper.CreateBlankImage(mappedDevice.ButtonResolution, DeviceColor.Cyan);
            mappedDevice.SetKey(mappedCommand.ButtonIndex, cyanImage);
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
                bitmap.Save(ms, ImageFormat.Png);
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
