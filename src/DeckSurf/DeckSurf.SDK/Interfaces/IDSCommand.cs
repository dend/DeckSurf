// Copyright (c) Den Delimarsky
// Den Delimarsky licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using DeckSurf.SDK.Models;

namespace DeckSurf.SDK.Interfaces
{
    public interface IDSCommand
    {
        public string Name { get; }

        public string Description { get; }

        public void ExecuteOnActivation(CommandMapping mappedCommand, ConnectedDevice mappedDevice);

        public void ExecuteOnAction(CommandMapping mappedCommand, ConnectedDevice mappedDevice, int activatingButton = -1);
    }
}
