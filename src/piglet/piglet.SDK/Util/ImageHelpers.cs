// Copyright (c) Den Delimarsky
// Den Delimarsky licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Piglet.SDK.Util
{
    public class ImageHelpers
    {
        public static byte[] ResizeImage(string path, int width, int height)
        {
            if (File.Exists(path))
            {
                Image currentImage = Image.FromFile(path);

                var targetRectangle = new Rectangle(0, 0, width, height);
                var targetImage = new Bitmap(width, height);

                targetImage.SetResolution(currentImage.HorizontalResolution, currentImage.VerticalResolution);

                using (var graphics = Graphics.FromImage(targetImage))
                {
                    graphics.CompositingMode = CompositingMode.SourceCopy;
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.SmoothingMode = SmoothingMode.HighQuality;
                    graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                    using var wrapMode = new ImageAttributes();
                    wrapMode.SetWrapMode(WrapMode.TileFlipXY);
                    graphics.DrawImage(currentImage, targetRectangle, 0, 0, currentImage.Width, currentImage.Height, GraphicsUnit.Pixel, wrapMode);
                }

                var converter = new ImageConverter();
                return (byte[])converter.ConvertTo(targetImage, typeof(byte[]));
            }
            else
            {
                throw new FileNotFoundException("File not found. Make sure the path is correct.");
            }
        }
    }
}
