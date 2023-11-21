using DeckSurf.SDK.Core;
using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using DeckSurf.SDK.Util;
using GregsStack.InputSimulatorStandard;
using GregsStack.InputSimulatorStandard.Native;
using System;
using System.Diagnostics;
using System.Linq;

namespace DeckSurf.Plugin.Barn.Commands
{
    [CompatibleWith(DeviceModel.ORIGINAL_V2)]
    class ExecuteKeystroke : IDSCommand
    {
        public string Name => "Execute Keystroke";
        public string Description => "Presses a single key on the keyboard";
        private readonly InputSimulator _simulator = new InputSimulator();

        public void ExecuteOnAction(CommandMapping mappedCommand, ConnectedDevice mappedDevice, int activatingButton = -1)
        {
            var args = mappedCommand.CommandArguments.Split(" ");

            //TODO Extend logic to look for additional modifier keys to use, e.g. control+shift+F
            var modifierKeyName = "";
            if (args.Length >= 1)
            {
                modifierKeyName = args.First();
            }

            var keyName = args.Last();

            var modifierKeycode = (VirtualKeyCode)Enum.Parse(typeof(VirtualKeyCode), modifierKeyName);
            var keyCode = (VirtualKeyCode)Enum.Parse(typeof(VirtualKeyCode), keyName);

            _simulator.Keyboard.ModifiedKeyStroke(modifierKeycode, keyCode);
        }

        public void ExecuteOnActivation(CommandMapping mappedCommand, ConnectedDevice mappedDevice)
        {
            if (string.IsNullOrEmpty(mappedCommand.ButtonImagePath))
            {
                try
                {
                    ImageHelpers.GetDeviceIconSizes(mappedDevice.Model, out var width, out var height, out var emSize);

                    var icon = ImageHelpers.GetFileIcon(mappedCommand.CommandArguments, DeviceConstants.UniversalButtonSize, DeviceConstants.UniversalButtonSize, SIIGBF.SIIGBF_ICONONLY | SIIGBF.SIIGBF_CROPTOSQUARE);
                    var byteContent = ImageHelpers.GetImageBuffer(icon);

                    var resizedByteContent = ImageHelpers.ResizeImage(byteContent, width, height);
                    mappedDevice.SetKey(mappedCommand.ButtonIndex, resizedByteContent);
                }
                catch
                {
                    Debug.WriteLine($"Could not set icon for {mappedCommand.CommandArguments}");
                }
            }
        }
    }
}
