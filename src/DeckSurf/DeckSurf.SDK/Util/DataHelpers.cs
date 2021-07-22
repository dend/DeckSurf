// Copyright (c) Den Delimarsky
// Den Delimarsky licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace DeckSurf.SDK.Util
{
    public class DataHelpers
    {
        public static string ByteArrayToString(byte[] data)
        {
            return BitConverter.ToString(data);
        }
    }
}
