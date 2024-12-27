using DeckSurf.Plugin.Barn.Helpers;
using DeckSurf.SDK.Core;
using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using DeckSurf.SDK.Util;
using System;
using System.Diagnostics;
using System.Drawing;
using System.Threading;

namespace DeckSurf.Plugin.Barn.Commands
{
    [CompatibleWith(DeviceModel.XL)]
    class ShowCPUUsage : IDSCommand
    {
        private const string CategoryName = "Processor";
        private const string CounterName = "% Processor Time";
        private const string InstanceName = "_Total";

        public string Name => "Show CPU Usage";
        public string Description => "Shows % of the CPU being used.";

        public void ExecuteOnAction(CommandMapping mappedCommand, ConnectedDevice mappedDevice, int activatingButton = -1)
        {
            
        }

        public void ExecuteOnActivation(CommandMapping mappedCommand, ConnectedDevice mappedDevice)
        {
            var cpuUsageTimer = new System.Timers.Timer(2000);
            cpuUsageTimer.Elapsed += (s, e) =>
            {
                var randomIconFromText = IconGenerator.GenerateTestImageFromText(GetCPUUsage().ToString() + "%", new Font("Bahnschrift", 94), Color.Red, Color.Black);
                var resizeImage = ImageHelpers.ResizeImage(ImageHelpers.GetImageBuffer(randomIconFromText), mappedDevice.ButtonResolution, mappedDevice.ButtonResolution, mappedDevice.IsButtonImageFlipRequired);

                mappedDevice.SetKey(mappedCommand.ButtonIndex, resizeImage);
            };
            cpuUsageTimer.Start();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "Intended to work on Windows only at this time.")]
        private static int GetCPUUsage()
        {
            PerformanceCounter perfCounter = new(categoryName: CategoryName, counterName: CounterName, instanceName: InstanceName);
            // Dummy call because PerformanceCounter will always start with zero.
            perfCounter.NextValue();
            Thread.Sleep(1000);
            var targetCPUUsage = (int)Math.Round(perfCounter.NextValue());
            return targetCPUUsage;
        }
    }
}
