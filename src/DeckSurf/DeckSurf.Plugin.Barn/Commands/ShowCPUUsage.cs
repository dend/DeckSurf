using DeckSurf.Plugin.Barn.Helpers;
using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using DeckSurf.SDK.Util;
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

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
    class ShowCPUUsage : IDeckSurfCommand
    {
        private System.Timers.Timer _cpuUsageTimer;

        public string Name => "Show CPU Usage";
        public string Description => "Displays live CPU usage percentage on a Stream Deck button.";

        public void ExecuteOnAction(CommandMapping mappedCommand, IConnectedDevice mappedDevice, int activatingButton = -1)
        {
        }

        public void ExecuteOnActivation(CommandMapping mappedCommand, IConnectedDevice mappedDevice)
        {
            _cpuUsageTimer = new System.Timers.Timer(2000);
            _cpuUsageTimer.Elapsed += (s, e) =>
            {
                try
                {
                    int cpuUsage = CpuMonitor.GetSystemCpuUsage();
                    if (cpuUsage < 0) return;

                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        RenderTextButton(cpuUsage, mappedCommand, mappedDevice);
                    }
                    else
                    {
                        RenderColorButton(cpuUsage, mappedCommand, mappedDevice);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in CPU usage timer callback: {ex}");
                }
            };
            _cpuUsageTimer.Start();
        }

        [System.Runtime.Versioning.SupportedOSPlatform("windows")]
        private static void RenderTextButton(int cpuUsage, CommandMapping mappedCommand, IConnectedDevice mappedDevice)
        {
            using var image = IconGenerator.GenerateTestImageFromText(
                cpuUsage + "%",
                new System.Drawing.Font("Bahnschrift", 94),
                System.Drawing.Color.Red,
                System.Drawing.Color.Black);

            byte[] byteContent;
            using (var ms = new MemoryStream())
            {
                image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                byteContent = ms.ToArray();
            }

            var resized = ImageHelper.ResizeImage(
                byteContent,
                mappedDevice.ButtonResolution,
                mappedDevice.ButtonResolution,
                mappedDevice.ImageRotation,
                mappedDevice.KeyImageFormat);

            mappedDevice.SetKey(mappedCommand.ButtonIndex, resized);
        }

        private static void RenderColorButton(int cpuUsage, CommandMapping mappedCommand, IConnectedDevice mappedDevice)
        {
            // Map CPU usage to a green (low) -> yellow (mid) -> red (high) gradient.
            var color = CpuUsageToColor(cpuUsage);
            mappedDevice.SetKeyColor(mappedCommand.ButtonIndex, color);
        }

        private static DeviceColor CpuUsageToColor(int percent)
        {
            percent = Math.Clamp(percent, 0, 100);

            // 0% = green (0,180,0), 50% = yellow (255,200,0), 100% = red (255,0,0)
            byte r, g, b = 0;
            if (percent <= 50)
            {
                double t = percent / 50.0;
                r = (byte)(t * 255);
                g = (byte)(180 + t * 20);
            }
            else
            {
                double t = (percent - 50) / 50.0;
                r = 255;
                g = (byte)(200 * (1 - t));
            }

            return new DeviceColor(r, g, b);
        }

        public void Dispose()
        {
            _cpuUsageTimer?.Stop();
            _cpuUsageTimer?.Dispose();
        }
    }
}
