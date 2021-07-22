// Copyright (c) Den Delimarsky
// Den Delimarsky licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace DeckSurf.SDK.Models
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = true)]
    public class CompatibleWithAttribute : Attribute
    {
        public CompatibleWithAttribute(DeviceModel model)
        {
            this.CompatibleModel = model;
        }

        public DeviceModel CompatibleModel { get; set; }
    }
}
