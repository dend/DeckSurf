using piglet.SDK.Models;
using System;
using System.Collections.Generic;

namespace piglet.SDK.Interfaces
{
    public interface IPlugin
    {
        public PluginMetadata Metadata { get; }
        public List<Type> GetSupportedCommands();
    }
}
