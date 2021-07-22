// Copyright (c) Den Delimarsky
// Den Delimarsky licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace DeckSurf.SDK.Models
{
    public class ConfigurationProfile
    {
        public ConfigurationProfile()
        {
            this.ButtonMap = new List<CommandMapping>();
        }

        [JsonPropertyName("device_index")]
        public int DeviceIndex { get; set; }

        [JsonPropertyName("button_map")]
        public List<CommandMapping> ButtonMap { get; set; }
    }
}
