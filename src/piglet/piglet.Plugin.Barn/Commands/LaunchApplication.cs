using Piglet.SDK.Interfaces;
using Piglet.SDK.Models;
using System;
using System.Diagnostics;

namespace Piglet.Plugin.Barn.Commands
{
    [CompatibleWith(DeviceModel.XL)]
    [ExpectedInteractionFormat(CommandType.ACTION)]
    class LaunchApplication : IPigletCommand
    {
        public string Name => "Launch Application";
        public string Description => "Launches an application on the machine.";

        public void ExecuteOnAction(int keyIndex, string arguments)
        {
            Process.Start(arguments);
        }

        public void ExecuteOnActivation(int keyIndex, string arguments)
        {
            throw new NotImplementedException();
        }
    }
}
