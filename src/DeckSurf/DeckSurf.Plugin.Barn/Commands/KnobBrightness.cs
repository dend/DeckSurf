using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using System;

namespace DeckSurf.Plugin.Barn.Commands
{
    [CompatibleWith(DeviceModel.Plus)]
    [CommandParameter("step", CommandParameterType.Integer, DisplayName = "Step per tick", Description = "How much the brightness changes per knob rotation tick.", DefaultValue = "5", MinValue = 1, MaxValue = 25)]
    class KnobBrightness : IDeckSurfCommand
    {
        private int _level = 60;
        private int _lastNonZeroLevel = 60;

        public string Name => "Knob brightness";

        public string Description => "Adjusts device brightness with a knob: rotate to change, press to toggle the backlight.";

        public void ExecuteOnActivation(CommandMapping mappedCommand, IConnectedDevice mappedDevice)
        {
            mappedDevice.SetBrightness((byte)_level);
        }

        public void ExecuteOnAction(CommandMapping mappedCommand, IConnectedDevice mappedDevice, int activatingButton = -1)
        {
            // Knob press: toggle the backlight on/off.
            if (_level > 0)
            {
                _lastNonZeroLevel = _level;
                _level = 0;
            }
            else
            {
                _level = _lastNonZeroLevel;
            }

            mappedDevice.SetBrightness((byte)_level);
        }

        public void ExecuteOnEvent(CommandMapping mappedCommand, IConnectedDevice mappedDevice, ButtonPressEventArgs eventArgs)
        {
            if (eventArgs.IsKnobRotating == true)
            {
                var step = mappedCommand.CommandArguments.GetInt32("step", 5);
                var delta = eventArgs.KnobRotationDirection == KnobRotationDirection.Right ? step : -step;
                _level = Math.Clamp(_level + delta, 0, 100);
                mappedDevice.SetBrightness((byte)_level);
                return;
            }

            if (eventArgs.EventKind == ButtonEventKind.Down)
            {
                ExecuteOnAction(mappedCommand, mappedDevice, eventArgs.Id);
            }
        }

        public void Dispose()
        {
        }
    }
}
