using DeckSurf.Plugin.Barn.Commands;
using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using System;
using System.Collections.Generic;

namespace DeckSurf.Plugin.Barn
{
    public class Plugin : IDSPlugin
    {
        private PluginMetadata _metadata = new()
        {
            Author = "Den Delimarsky",
            Id = "DeckSurf.Plugin.Barn",
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
                typeof(LaunchApplication),
                typeof(ShowCPUUsage),
                typeof(SnakeGame)
            };
        }
    }
}
