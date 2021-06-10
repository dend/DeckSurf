using System.Text.Json.Serialization;

namespace piglet.SDK.Models
{
    public class CommandMapping
    {
        [JsonPropertyName("plugin")]
        public string Plugin { get; set; }
        [JsonPropertyName("command")]
        public string Command { get; set; }
        [JsonPropertyName("command_arguments")]
        public string CommandArguments { get; set; }
        [JsonPropertyName("button_index")]
        public int ButtonIndex { get; set; }
        [JsonPropertyName("button_image_path")]
        public string ButtonImagePath { get; set; }
    }
}
