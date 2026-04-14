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
    class ShowRAMUsage : IDeckSurfCommand
    {
        private const int MaxHistory = 30;

        private System.Timers.Timer _ramUsageTimer;
        private readonly List<int> _history = new();
        private readonly object _historyLock = new();

        public string Name => "Show RAM Usage";
        public string Description => "Displays live RAM usage percentage on a Stream Deck button.";

        public void ExecuteOnAction(CommandMapping mappedCommand, IConnectedDevice mappedDevice, int activatingButton = -1)
        {
        }

        public void ExecuteOnActivation(CommandMapping mappedCommand, IConnectedDevice mappedDevice)
        {
            _ramUsageTimer = new System.Timers.Timer(2000);
            _ramUsageTimer.Elapsed += (s, e) =>
            {
                try
                {
                    int ramUsage = MemoryMonitor.GetSystemMemoryUsagePercent();
                    if (ramUsage < 0) return;

                    lock (_historyLock)
                    {
                        _history.Add(ramUsage);
                        if (_history.Count > MaxHistory)
                            _history.RemoveAt(0);
                    }

                    RenderButton(ramUsage, mappedCommand, mappedDevice);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in RAM usage timer callback: {ex}");
                }
            };
            _ramUsageTimer.Start();
        }

        private void RenderButton(int ramUsage, CommandMapping mappedCommand, IConnectedDevice mappedDevice)
        {
            var font = IconGenerator.ResolveFont(36, SixLabors.Fonts.FontStyle.Bold);

            List<int> snapshot;
            lock (_historyLock)
            {
                snapshot = new List<int>(_history);
            }

            using var image = IconGenerator.GenerateUsageImage(
                200,
                "RAM",
                ramUsage + "%",
                font,
                snapshot);

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
            _ramUsageTimer?.Stop();
            _ramUsageTimer?.Dispose();
        }
    }
}
