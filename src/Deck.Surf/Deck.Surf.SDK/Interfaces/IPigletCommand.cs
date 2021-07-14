// Copyright (c) Den Delimarsky
// Den Delimarsky licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Deck.Surf.SDK.Interfaces
{
    public interface IPigletCommand
    {
        public string Name { get; }

        public string Description { get; }

        public void ExecuteOnActivation(int keyIndex, string arguments);

        public void ExecuteOnAction(int keyIndex, string arguments);
    }
}
