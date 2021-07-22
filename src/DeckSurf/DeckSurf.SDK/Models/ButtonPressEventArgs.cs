// Copyright (c) Den Delimarsky
// Den Delimarsky licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace DeckSurf.SDK.Models
{
    public class ButtonPressEventArgs : EventArgs
    {
        public ButtonPressEventArgs(int id, ButtonEventKind kind)
        {
            this.Id = id;
            this.Kind = kind;
        }

        public int Id { get; }

        public ButtonEventKind Kind { get; }
    }
}
