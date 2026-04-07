using DeckSurf.Plugin.Barn.Commands;
using DeckSurf.SDK.Interfaces;
using DeckSurf.SDK.Models;
using System;
using System.Collections.Generic;

namespace DeckSurf.Plugin.Barn
{
    public class Plugin : IDeckSurfPlugin
    {
        private PluginMetadata _metadata = new()
        {
            Author = "Den Delimarsky",
            Id = "DeckSurf.Plugin.Barn",
            Version = "0.0.2",
            Website = "https://github.com/dend/decksurf"
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
