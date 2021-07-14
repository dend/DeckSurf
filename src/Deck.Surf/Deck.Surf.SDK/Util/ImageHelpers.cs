// Copyright (c) Den Delimarsky
// Den Delimarsky licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;

namespace Deck.Surf.SDK.Util
{
    public class ImageHelpers
    {
        public static byte[] ResizeImage(byte[] buffer, int width, int height)
        {
            Image currentImage = GetImage(buffer);

            var targetRectangle = new Rectangle(0, 0, width, height);
            var targetImage = new Bitmap(width, height, PixelFormat.Format24bppRgb);

            targetImage.SetResolution(currentImage.HorizontalResolution, currentImage.VerticalResolution);

            using (var graphics = Graphics.FromImage(targetImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                graphics.DrawImage(currentImage, targetRectangle, 0, 0, currentImage.Width, currentImage.Height, GraphicsUnit.Pixel);
            }

            // TODO: I am not sure if every image needs to be rotated, but
            // in my limited experiments, this seems to be the case.
            targetImage.RotateFlip(RotateFlipType.Rotate180FlipX);

            using var bufferStream = new MemoryStream();
            targetImage.Save(bufferStream, ImageFormat.Jpeg);
            return bufferStream.ToArray();
        }

        public static Image GetImage(byte[] buffer)
        {
            Image image = null;
            using (MemoryStream ms = new(buffer))
            {
                image = Image.FromStream(ms);
            }

            return image;
        }

        public static byte[] GetImageBuffer(Image image)
        {
            ImageConverter converter = new();
            byte[] buffer = (byte[])converter.ConvertTo(image, typeof(byte[]));
            return buffer;
        }
    }
}
