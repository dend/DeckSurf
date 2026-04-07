using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace DeckSurf.Plugin.Barn.Helpers
{
    internal class IconGenerator
    {
        internal static Image GenerateTestImageFromText(string text, Font font, Color textColor, Color backgroundColor)
        {
            var textOptions = new TextOptions(font);
            var textSize = TextMeasurer.MeasureSize(text, textOptions);

            int width = (int)textSize.Width;
            int height = (int)textSize.Height;

            var image = new Image<Rgba32>(width, height);
            image.Mutate(ctx =>
            {
                ctx.Fill(backgroundColor);
                ctx.DrawText(text, font, textColor, new PointF(0, 0));
            });

            return image;
        }
    }
}
