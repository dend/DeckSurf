using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace piglet.SDK.Models
{
    public class ConfigurationProfile
    {
        [JsonPropertyName("device_index")]
        public int DeviceIndex { get; set; }
        [JsonPropertyName("button_map")]
        public List<CommandMapping> ButtonMap { get; set; }

        public ConfigurationProfile()
        {
            ButtonMap = new List<CommandMapping>();
        }
    }
}
