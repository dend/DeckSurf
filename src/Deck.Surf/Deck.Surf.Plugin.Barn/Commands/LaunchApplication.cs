using Deck.Surf.SDK.Interfaces;
using Deck.Surf.SDK.Models;
using System;
using System.Diagnostics;

namespace Deck.Surf.Plugin.Barn.Commands
{
    [CompatibleWith(DeviceModel.XL)]
    class LaunchApplication : IDSCommand
    {
        public string Name => "Launch Application";
        public string Description => "Launches an application on the machine.";

        public void ExecuteOnAction(CommandMapping mappedCommand, ConnectedDevice mappedDevice)
        {
            Process.Start(mappedCommand.CommandArguments);
        }

        public void ExecuteOnActivation(CommandMapping mappedCommand, ConnectedDevice mappedDevice)
        {
            
        }
    }
}
