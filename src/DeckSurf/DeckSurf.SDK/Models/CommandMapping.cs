// Copyright (c) Den Delimarsky
// Den Delimarsky licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json.Serialization;

namespace DeckSurf.SDK.Models
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
