using Deck.Surf.Plugin.Barn.Commands;
using Deck.Surf.SDK.Interfaces;
using Deck.Surf.SDK.Models;
using System;
using System.Collections.Generic;

namespace Deck.Surf.Plugin.Barn
{
    public class Plugin : IDSPlugin
    {
        private PluginMetadata _metadata = new()
        {
            Author = "Den Delimarsky",
            Id = "Deck.Surf.Plugin.Barn",
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
