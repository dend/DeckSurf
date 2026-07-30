using DeckSurf.Plugin.Barn.Helpers;
using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace DeckSurf.Plugin.Barn.Commands
{
    [CommandDynamicDisplay]
    class ShowCPUUsage : IDeckSurfCommand
    {
        private EventHandler _sampleHandler;

        public string Name => "Show CPU usage";
        public string Description => "Displays live CPU usage percentage on a Stream Deck button.";

        public void ExecuteOnAction(CommandMapping mappedCommand, IConnectedDevice mappedDevice, int activatingButton = -1)
        {
        }

        public void ExecuteOnActivation(CommandMapping mappedCommand, IConnectedDevice mappedDevice)
        {
            // Rendering rides the shared sampler so every instance of this
            // command, across all connected devices, draws the same series.
            _sampleHandler = (s, e) =>
            {
                try
                {
                    var (cpuUsage, series) = SystemSampler.GetCpu();
                    if (cpuUsage < 0) return;

                    RenderButton(cpuUsage, series, mappedCommand, mappedDevice);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error rendering CPU usage: {ex}");
                }
            };
            SystemSampler.SampleAvailable += _sampleHandler;
        }

        private void RenderButton(int cpuUsage, List<int> series, CommandMapping mappedCommand, IConnectedDevice mappedDevice)
        {
            var font = IconGenerator.ResolveFont(36, SixLabors.Fonts.FontStyle.Bold);

            using var image = IconGenerator.GenerateUsageImage(
                200,
                "CPU",
                cpuUsage + "%",
                font,
                series);

            byte[] byteContent;
            using (var ms = new MemoryStream())
            {
                image.SaveAsPng(ms);
                byteContent = ms.ToArray();
            }

            mappedDevice.SetKey(mappedCommand.ButtonIndex, byteContent);
        }

        public void Dispose()
        {
            if (_sampleHandler != null)
            {
                SystemSampler.SampleAvailable -= _sampleHandler;
                _sampleHandler = null;
            }
        }
    }
}
