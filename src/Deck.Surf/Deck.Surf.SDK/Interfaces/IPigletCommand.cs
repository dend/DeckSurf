// Copyright (c) Den Delimarsky
// Den Delimarsky licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Deck.Surf.SDK.Models;

namespace Deck.Surf.SDK.Interfaces
{
    public interface IPigletCommand
    {
        public string Name { get; }

        public string Description { get; }

        public void ExecuteOnActivation(CommandMapping mappedCommand, ConnectedDevice mappedDevice);

        public void ExecuteOnAction(CommandMapping mappedCommand, ConnectedDevice mappedDevice);
    }
}
