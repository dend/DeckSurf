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
    class ShowNetworkTraffic : IDeckSurfCommand
    {
        private EventHandler _sampleHandler;

        public string Name => "Show network traffic";
        public string Description => "Displays live network upload/download speeds on a Stream Deck button.";

        public void ExecuteOnAction(CommandMapping mappedCommand, IConnectedDevice mappedDevice, int activatingButton = -1)
        {
        }

        public void ExecuteOnActivation(CommandMapping mappedCommand, IConnectedDevice mappedDevice)
        {
            // This command draws on a grid key or the touch strip; any other
            // target has no face for it and must not paint the key sharing its
            // index.
            var paintsKey = mappedCommand.Target == MappingTarget.Key && mappedCommand.ButtonIndex >= 0;
            var paintsScreen = mappedCommand.Target == MappingTarget.Screen && mappedDevice.IsScreenSupported;
            if (!paintsKey && !paintsScreen)
                return;

            // Rendering rides the shared sampler so every instance of this
            // command, across all connected devices, draws the same series.
            _sampleHandler = (s, e) =>
            {
                try
                {
                    var (up, down, series) = SystemSampler.GetNetwork();
                    if (up < 0) return;

                    RenderButton(up, down, series, mappedCommand, mappedDevice);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error rendering network traffic: {ex}");
                }
            };
            SystemSampler.SampleAvailable += _sampleHandler;
        }

        private void RenderButton(long up, long down, List<long> series, CommandMapping mappedCommand, IConnectedDevice mappedDevice)
        {
            var font = IconGenerator.ResolveFont(28, SixLabors.Fonts.FontStyle.Bold);

            // Normalize history to 0-100 based on peak value in the window.
            var normalized = new List<int>(series.Count);
            if (series.Count > 0)
            {
                long max = 0;
                foreach (var v in series)
                    if (v > max) max = v;

                foreach (var v in series)
                    normalized.Add(max > 0 ? (int)(v * 100 / max) : 0);
            }

            string upLabel = "\u25b2 " + NetworkMonitor.FormatBytes(up);
            string downLabel = "\u25bc " + NetworkMonitor.FormatBytes(down);

            // Render to whatever the mapping actually targets: a screen mapping
            // paints the touch strip, everything else paints its key. Without the
            // branch, a screen-assigned instance painted key ButtonIndex instead,
            // fighting that key's own content.
            if (mappedCommand.Target == MappingTarget.Screen && mappedDevice.IsScreenSupported)
            {
                using var strip = IconGenerator.GenerateNetworkStripImage(
                    mappedDevice.ScreenWidth,
                    mappedDevice.ScreenHeight,
                    "NET",
                    upLabel,
                    downLabel,
                    font,
                    normalized);

                byte[] stripContent;
                using (var ms = new MemoryStream())
                {
                    strip.SaveAsJpeg(ms);
                    stripContent = ms.ToArray();
                }

                mappedDevice.SetScreen(stripContent, 0, mappedDevice.ScreenWidth, mappedDevice.ScreenHeight);
                return;
            }

            using var image = IconGenerator.GenerateNetworkImage(
                200,
                "NET",
                upLabel,
                downLabel,
                font,
                normalized);

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
