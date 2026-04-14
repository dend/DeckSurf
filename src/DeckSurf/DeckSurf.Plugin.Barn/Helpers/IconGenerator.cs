using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Linq;

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

        internal static Image GenerateUsageImage(int size, string title, string label, Font font, IReadOnlyList<int> history)
        {
            var image = new Image<Rgba32>(size, size);
            var titleFont = ResolveFont(font.Size * 0.5f);

            image.Mutate(ctx =>
            {
                ctx.Fill(Color.Black);

                // Draw the history graph if we have data.
                if (history.Count > 1)
                {
                    DrawGraph(ctx, size, history);
                }

                // Draw the title near the top.
                var titleOptions = new RichTextOptions(titleFont)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Origin = new PointF(size / 2f, size * 0.08f),
                };

                ctx.DrawText(titleOptions, title, Color.White);

                // Draw the percentage label centered, shifted down slightly.
                var labelOptions = new RichTextOptions(font)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Origin = new PointF(size / 2f, size * 0.55f),
                };

                ctx.DrawText(labelOptions, label, Color.White);
            });

            return image;
        }

        private static void DrawGraph(IImageProcessingContext ctx, int size, IReadOnlyList<int> history)
        {
            int count = history.Count;
            float barWidth = (float)size / count;
            int graphTop = (int)(size * 0.15f);
            int graphHeight = size - graphTop;

            for (int i = 0; i < count; i++)
            {
                int value = Math.Clamp(history[i], 0, 100);
                float barHeight = (value / 100f) * graphHeight;
                float x = i * barWidth;
                float y = size - barHeight;

                var color = ValueToColor(value);
                var rect = new RectangularPolygon(x, y, barWidth + 0.5f, barHeight);
                ctx.Fill(color, rect);
            }
        }

        private static Color ValueToColor(int percent)
        {
            // Green (low) -> Yellow (mid) -> Red (high), at ~40% opacity.
            float t = percent / 100f;
            byte r, g;

            if (t <= 0.5f)
            {
                float s = t / 0.5f;
                r = (byte)(s * 255);
                g = (byte)(180 + s * 75);
            }
            else
            {
                float s = (t - 0.5f) / 0.5f;
                r = 255;
                g = (byte)(255 * (1 - s));
            }

            return Color.FromRgba(r, g, 0, 100);
        }

        internal static Image GenerateNetworkImage(int size, string title, string upLabel, string downLabel, Font font, IReadOnlyList<int> history)
        {
            var image = new Image<Rgba32>(size, size);
            var titleFont = ResolveFont(font.Size * 0.55f);

            image.Mutate(ctx =>
            {
                ctx.Fill(Color.Black);

                if (history.Count > 1)
                {
                    DrawGraph(ctx, size, history);
                }

                // Title near the top.
                var titleOptions = new RichTextOptions(titleFont)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Origin = new PointF(size / 2f, size * 0.06f),
                };
                ctx.DrawText(titleOptions, title, Color.White);

                // Upload line.
                var upOptions = new RichTextOptions(font)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Origin = new PointF(size / 2f, size * 0.42f),
                };
                ctx.DrawText(upOptions, upLabel, Color.White);

                // Download line.
                var downOptions = new RichTextOptions(font)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Origin = new PointF(size / 2f, size * 0.65f),
                };
                ctx.DrawText(downOptions, downLabel, Color.White);
            });

            return image;
        }

        internal static Image GenerateTimerImage(int size, string title, string timeText, Font font, bool flash = false)
        {
            var image = new Image<Rgba32>(size, size);
            var titleFont = ResolveFont(font.Size * 0.45f);

            var bgColor = flash && DateTime.UtcNow.Second % 2 == 0 ? Color.DarkRed : Color.Black;

            image.Mutate(ctx =>
            {
                ctx.Fill(bgColor);

                // Title near the top.
                var titleOptions = new RichTextOptions(titleFont)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Top,
                    Origin = new PointF(size / 2f, size * 0.10f),
                };
                ctx.DrawText(titleOptions, title, Color.White);

                // Time centered.
                var timeOptions = new RichTextOptions(font)
                {
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    Origin = new PointF(size / 2f, size * 0.55f),
                };
                ctx.DrawText(timeOptions, timeText, Color.White);
            });

            return image;
        }

        internal static Font ResolveFont(float size, FontStyle style = FontStyle.Regular)
        {
            string[] candidates = ["DejaVu Sans", "Liberation Sans", "Arial", "Segoe UI"];
            foreach (var name in candidates)
            {
                if (SystemFonts.TryGet(name, out var family))
                    return family.CreateFont(size, style);
            }

            return SystemFonts.Families.GetEnumerator().MoveNext()
                ? SystemFonts.Families.First().CreateFont(size, style)
                : throw new InvalidOperationException("No system fonts available.");
        }
    }
}
