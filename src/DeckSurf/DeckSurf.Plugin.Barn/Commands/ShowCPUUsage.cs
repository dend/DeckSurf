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
    class ShowCPUUsage : IDeckSurfCommand
    {
        private const int MaxHistory = 30;

        private System.Timers.Timer _cpuUsageTimer;
        private readonly List<int> _history = new();
        private readonly object _historyLock = new();

        public string Name => "Show CPU usage";
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

                    lock (_historyLock)
                    {
                        _history.Add(cpuUsage);
                        if (_history.Count > MaxHistory)
                            _history.RemoveAt(0);
                    }

                    RenderButton(cpuUsage, mappedCommand, mappedDevice);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in CPU usage timer callback: {ex}");
                }
            };
            _cpuUsageTimer.Start();
        }

        private void RenderButton(int cpuUsage, CommandMapping mappedCommand, IConnectedDevice mappedDevice)
        {
            var font = IconGenerator.ResolveFont(36, SixLabors.Fonts.FontStyle.Bold);

            List<int> snapshot;
            lock (_historyLock)
            {
                snapshot = new List<int>(_history);
            }

            using var image = IconGenerator.GenerateUsageImage(
                200,
                "CPU",
                cpuUsage + "%",
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
            _cpuUsageTimer?.Stop();
            _cpuUsageTimer?.Dispose();
        }
    }
}
