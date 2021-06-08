using piglet.SDK.Models;

namespace piglet.SDK.Interfaces
{
    public interface IPlugin
    {
        public PluginMetadata GetPluginMetadata();
    }
}
