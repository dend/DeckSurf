using piglet.SDK.Models;
using System.Threading.Tasks;

namespace piglet.SDK.Interfaces
{
    public interface IPlugin
    {
        public Task<bool> Execute(string command, string argument);
        public PluginMetadata GetPluginMetadata();
    }
}
