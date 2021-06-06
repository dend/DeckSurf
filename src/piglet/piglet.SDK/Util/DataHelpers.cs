using System;
using System.Text;

namespace piglet.SDK.Util
{
    public class DataHelpers
    {
        public static string ByteArrayToString(byte[] data)
        {
            return BitConverter.ToString(data);
        }
    }
}
