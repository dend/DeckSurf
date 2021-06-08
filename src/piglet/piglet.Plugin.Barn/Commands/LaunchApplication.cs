using piglet.SDK.Interfaces;
using piglet.SDK.Models;
using System;
using System.Diagnostics;

namespace piglet.Plugin.Barn.Commands
{
    [CompatibleWith(DeviceModel.XL)]
    class LaunchApplication : ICommand
    {
        public string Name
        {
            get
            {
                return "Launch Application";
            }
        }

        public string Description
        {
            get
            {
                return "Launches an application on the machine.";
            }
        }

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
