using Piglet.Plugin.Barn.Commands;
using Piglet.SDK.Interfaces;
using Piglet.SDK.Models;
using System;
using System.Collections.Generic;

namespace Piglet.Plugin.Barn
{
    public class Plugin : IPlugin
    {
        private PluginMetadata _metadata = new()
        {
            Author = "Den Delimarsky",
            Id = "piglet.Plugin.Barn",
            Version = "0.0.1-alpha",
            Website = "https://github.com/dend/piglet"
        };

        public PluginMetadata Metadata
        {
            get
            {
                return _metadata;
            }
        }

        public List<Type> GetSupportedCommands()
        {
            return new List<Type>()
            { 
                typeof(LaunchApplication) 
            };
        }
    }
}
