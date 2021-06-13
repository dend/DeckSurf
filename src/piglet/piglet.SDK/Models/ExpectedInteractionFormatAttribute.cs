// Copyright (c) Den Delimarsky
// Den Delimarsky licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace Piglet.SDK.Models
{
    [AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
    public class ExpectedInteractionFormatAttribute : Attribute
    {
        public ExpectedInteractionFormatAttribute(CommandType type)
        {
            this.CommandType = type;
        }

        private CommandType CommandType { get; set; }
    }
}
