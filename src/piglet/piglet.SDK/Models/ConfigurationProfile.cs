using System.Collections.Generic;

namespace piglet.SDK.Models
{
    public class ConfigurationProfile
    {
        public int DeviceIndex { get; set; }
        public List<CommandMapping> ButtonMap { get; set; }

        public ConfigurationProfile()
        {
            ButtonMap = new List<CommandMapping>();
        }
    }
}
