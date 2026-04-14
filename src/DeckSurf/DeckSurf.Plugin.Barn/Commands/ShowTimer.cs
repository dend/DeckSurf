using DeckSurf.Plugin.Barn.Helpers;
using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using SixLabors.ImageSharp;
using System;
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
    class ShowTimer : IDeckSurfCommand
    {
        private enum TimerMode { Clock, Stopwatch, Timer }

        private TimerMode _mode = TimerMode.Clock;
        private System.Timers.Timer _renderTimer;
        private readonly Stopwatch _stopwatch = new();
        private TimeSpan _countdownDuration;
        private DateTime _countdownStartedAt;
        private bool _isRunning;
        private bool _countdownExpired;
        private DateTime _lastPressTime;

        // Store references for re-rendering from ExecuteOnAction.
        private CommandMapping _mappedCommand;
        private IConnectedDevice _mappedDevice;

        public string Name => "Timer";
        public string Description => "Clock, stopwatch, or countdown timer on a Stream Deck button.";

        public void ExecuteOnAction(CommandMapping mappedCommand, IConnectedDevice mappedDevice, int activatingButton = -1)
        {
            if (_mode == TimerMode.Clock) return;

            var now = DateTime.UtcNow;
            bool isDoubleTap = (now - _lastPressTime).TotalMilliseconds < 400;
            _lastPressTime = now;

            if (isDoubleTap)
            {
                // Reset.
                _isRunning = false;
                _stopwatch.Reset();
                _countdownExpired = false;
                Render();
                return;
            }

            // Toggle start/stop.
            _isRunning = !_isRunning;

            if (_mode == TimerMode.Stopwatch)
            {
                if (_isRunning)
                    _stopwatch.Start();
                else
                    _stopwatch.Stop();
            }
            else if (_mode == TimerMode.Timer)
            {
                if (_isRunning)
                {
                    _countdownStartedAt = DateTime.UtcNow;
                    if (_stopwatch.Elapsed == TimeSpan.Zero)
                        _stopwatch.Restart();
                    else
                        _stopwatch.Start();
                    _countdownExpired = false;
                }
                else
                {
                    _stopwatch.Stop();
                }
            }

            Render();
        }

        public void ExecuteOnActivation(CommandMapping mappedCommand, IConnectedDevice mappedDevice)
        {
            _mappedCommand = mappedCommand;
            _mappedDevice = mappedDevice;

            ParseArguments(mappedCommand.CommandArguments);

            _renderTimer = new System.Timers.Timer(1000);
            _renderTimer.Elapsed += (s, e) =>
            {
                try
                {
                    Render();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error in timer render callback: {ex}");
                }
            };
            _renderTimer.Start();
        }

        private void ParseArguments(string args)
        {
            if (string.IsNullOrWhiteSpace(args)) return;

            foreach (var part in args.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var kv = part.Split('=', 2);
                if (kv.Length != 2) continue;

                var key = kv[0].Trim().ToLowerInvariant();
                var value = kv[1].Trim();

                switch (key)
                {
                    case "mode":
                        _mode = value.ToLowerInvariant() switch
                        {
                            "stopwatch" => TimerMode.Stopwatch,
                            "timer" => TimerMode.Timer,
                            _ => TimerMode.Clock,
                        };
                        break;
                    case "duration":
                        if (int.TryParse(value, out int seconds))
                            _countdownDuration = TimeSpan.FromSeconds(seconds);
                        break;
                }
            }
        }

        private void Render()
        {
            if (_mappedDevice == null) return;

            string title;
            string timeText;
            bool flash = false;

            switch (_mode)
            {
                case TimerMode.Clock:
                    title = "CLOCK";
                    var now = DateTime.Now;
                    timeText = now.ToString("HH:mm");
                    break;

                case TimerMode.Stopwatch:
                    title = "STOP\u200bWATCH";
                    var elapsed = _stopwatch.Elapsed;
                    timeText = elapsed.TotalHours >= 1
                        ? $"{(int)elapsed.TotalHours}:{elapsed.Minutes:D2}:{elapsed.Seconds:D2}"
                        : $"{elapsed.Minutes:D2}:{elapsed.Seconds:D2}";
                    break;

                case TimerMode.Timer:
                    title = "TIMER";
                    var remaining = _countdownDuration - _stopwatch.Elapsed;
                    if (remaining <= TimeSpan.Zero)
                    {
                        remaining = TimeSpan.Zero;
                        if (_isRunning)
                        {
                            _isRunning = false;
                            _stopwatch.Stop();
                            _countdownExpired = true;
                        }
                    }
                    timeText = remaining.TotalHours >= 1
                        ? $"{(int)remaining.TotalHours}:{remaining.Minutes:D2}:{remaining.Seconds:D2}"
                        : $"{remaining.Minutes:D2}:{remaining.Seconds:D2}";
                    flash = _countdownExpired;
                    break;

                default:
                    return;
            }

            var font = IconGenerator.ResolveFont(42, SixLabors.Fonts.FontStyle.Bold);

            using var image = IconGenerator.GenerateTimerImage(200, title, timeText, font, flash);

            byte[] byteContent;
            using (var ms = new MemoryStream())
            {
                image.SaveAsPng(ms);
                byteContent = ms.ToArray();
            }

            _mappedDevice.SetKey(_mappedCommand.ButtonIndex, byteContent);
        }

        public void Dispose()
        {
            _renderTimer?.Stop();
            _renderTimer?.Dispose();
        }
    }
}
