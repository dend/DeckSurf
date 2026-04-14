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
    [CompatibleWith(DeviceModel.XL)]
    [CompatibleWith(DeviceModel.XL2022)]
    [CompatibleWith(DeviceModel.Original)]
    [CompatibleWith(DeviceModel.Original2019)]
    [CompatibleWith(DeviceModel.MK2)]
    [CompatibleWith(DeviceModel.Mini)]
    [CompatibleWith(DeviceModel.Mini2022)]
    [CompatibleWith(DeviceModel.Plus)]
    [CompatibleWith(DeviceModel.Neo)]
    class ShowNetworkTraffic : IDeckSurfCommand
    {
        private const int MaxHistory = 30;

        private System.Timers.Timer _netTimer;
        private readonly List<long> _history = new();
        private readonly object _historyLock = new();

        public string Name => "Show Network Traffic";
        public string Description => "Displays live network upload/download speeds on a Stream Deck button.";

        public void ExecuteOnAction(CommandMapping mappedCommand, IConnectedDevice mappedDevice, int activatingButton = -1)
        {
        }

        public void ExecuteOnActivation(CommandMapping mappedCommand, IConnectedDevice mappedDevice)
        {
            _netTimer = new System.Timers.Timer(1000);
            _netTimer.Elapsed += (s, e) =>
            {
                try
                {
                    var (up, down) = NetworkMonitor.GetThroughput();
                    if (up < 0) return;

                    long total = up + down;

                    lock (_historyLock)
                    {
                        _history.Add(total);
                        if (_history.Count > MaxHistory)
                            _history.RemoveAt(0);
                    }

                    RenderButton(up, down, mappedCommand, mappedDevice);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in network traffic timer callback: {ex}");
                }
            };
            _netTimer.Start();
        }

        private void RenderButton(long up, long down, CommandMapping mappedCommand, IConnectedDevice mappedDevice)
        {
            var font = IconGenerator.ResolveFont(28, SixLabors.Fonts.FontStyle.Bold);

            List<long> snapshot;
            lock (_historyLock)
            {
                snapshot = new List<long>(_history);
            }

            // Normalize history to 0-100 based on peak value in the window.
            var normalized = new List<int>(snapshot.Count);
            if (snapshot.Count > 0)
            {
                long max = 0;
                foreach (var v in snapshot)
                    if (v > max) max = v;

                foreach (var v in snapshot)
                    normalized.Add(max > 0 ? (int)(v * 100 / max) : 0);
            }

            string upLabel = "\u25b2 " + NetworkMonitor.FormatBytes(up);
            string downLabel = "\u25bc " + NetworkMonitor.FormatBytes(down);

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
            _netTimer?.Stop();
            _netTimer?.Dispose();
        }
    }
}
