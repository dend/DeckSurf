using DeckSurf.Plugin.Barn.Helpers;
using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using DeckSurf.SDK.Util;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;

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
        private const string CategoryName = "Processor";
        private const string CounterName = "% Processor Time";
        private const string InstanceName = "_Total";

        private System.Timers.Timer _cpuUsageTimer;

        public string Name => "Show CPU Usage";
        public string Description => "Shows % of the CPU being used.";

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
                    using var randomIconFromText = IconGenerator.GenerateTestImageFromText(GetCPUUsage().ToString() + "%", new Font("Bahnschrift", 94), Color.Red, Color.Black);

                    byte[] byteContent;
                    using (var ms = new MemoryStream())
                    {
                        randomIconFromText.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        byteContent = ms.ToArray();
                    }

                    var resizeImage = ImageHelper.ResizeImage(byteContent, mappedDevice.ButtonResolution, mappedDevice.ButtonResolution, mappedDevice.ImageRotation, mappedDevice.KeyImageFormat);

                    mappedDevice.SetKey(mappedCommand.ButtonIndex, resizeImage);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in CPU usage timer callback: {ex}");
                }
            };
            _cpuUsageTimer.Start();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Intended to work on Windows only at this time.")]
        private static int GetCPUUsage()
        {
            using PerformanceCounter perfCounter = new(categoryName: CategoryName, counterName: CounterName, instanceName: InstanceName);
            // Dummy call because PerformanceCounter will always start with zero.
            perfCounter.NextValue();
            Thread.Sleep(1000);
            var targetCPUUsage = (int)Math.Round(perfCounter.NextValue());
            return targetCPUUsage;
        }

        public void Dispose()
        {
            _cpuUsageTimer?.Stop();
            _cpuUsageTimer?.Dispose();
        }
    }
}
