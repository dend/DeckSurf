using DeckSurf.Plugin.Barn.Helpers;
using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using DeckSurf.SDK.Util;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

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

                    RenderTextButton(cpuUsage, mappedCommand, mappedDevice);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in CPU usage timer callback: {ex}");
                }
            };
            _cpuUsageTimer.Start();
        }

        private static void RenderTextButton(int cpuUsage, CommandMapping mappedCommand, IConnectedDevice mappedDevice)
        {
            var font = ResolveFont(94);

            using var image = IconGenerator.GenerateTestImageFromText(
                cpuUsage + "%",
                font,
                Color.Red,
                Color.Black);

            byte[] byteContent;
            using (var ms = new MemoryStream())
            {
                image.SaveAsPng(ms);
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

        private static Font ResolveFont(float size)
        {
            string[] candidates = ["DejaVu Sans", "Liberation Sans", "Arial", "Segoe UI"];
            foreach (var name in candidates)
            {
                if (SystemFonts.TryGet(name, out var family))
                    return family.CreateFont(size);
            }

            return SystemFonts.Families.GetEnumerator().MoveNext()
                ? SystemFonts.Families.First().CreateFont(size)
                : throw new InvalidOperationException("No system fonts available.");
        }

        public void Dispose()
        {
            _cpuUsageTimer?.Stop();
            _cpuUsageTimer?.Dispose();
        }
    }
}
