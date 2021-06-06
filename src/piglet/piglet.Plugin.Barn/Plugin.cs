using piglet.SDK.Interfaces;
using piglet.SDK.Models;
using System;
using System.Threading.Tasks;

namespace piglet.Plugin.Barn
{
    public class Plugin : IPlugin
    {
        public Task<bool> Execute(string command, string argument)
        {
            throw new NotImplementedException();
        }

        public PluginMetadata GetPluginMetadata()
        {
            throw new NotImplementedException();
        }
    }
}
