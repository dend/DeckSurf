using System.Drawing;
using System.Drawing.Text;

namespace DeckSurf.Plugin.Barn.Helpers
{
    internal class IconGenerator
    {
        // Adapted from this snippet: https://stackoverflow.com/a/2070493/303696
        internal static Image GenerateTestImageFromText(string text, Font font, Color textColor, Color backgroundColor)
        {
            Image img = new Bitmap(1, 1);
            Graphics drawing = Graphics.FromImage(img);

            SizeF textSize = drawing.MeasureString(text, font);

            img.Dispose();
            drawing.Dispose();

            img = new Bitmap((int)textSize.Width, (int)textSize.Height);

            drawing = Graphics.FromImage(img);

            drawing.Clear(backgroundColor);

            Brush textBrush = new SolidBrush(textColor);

            drawing.TextRenderingHint = TextRenderingHint.AntiAlias;
            drawing.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            drawing.DrawString(text, font, textBrush, 0, 0);

            drawing.Save();

            textBrush.Dispose();
            drawing.Dispose();

            return img;
        }
    }
}
