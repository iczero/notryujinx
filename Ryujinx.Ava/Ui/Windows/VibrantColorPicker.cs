using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace Ryujinx.Ava.Ui.Windows
{
    class VibrantColorPicker
    {
        private const int PixelsPerAxis = 16;
        private const int TotalPixels = PixelsPerAxis * PixelsPerAxis;

        private const int RgbQuantBits = 5;
        private const int RgbQuantShift = BitsPerComponent - RgbQuantBits;

        private const int SatQuantBits = 5;
        private const int SatQuantShift = BitsPerComponent - SatQuantBits;

        private const int BitsPerComponent = 8;

        private const int CutOffLuminosity = 64;
        private const int CutOffPenaltyShift = 2;

        public static Color GetFilteredColor(Bitmap image)
        {
            var color = GetColor(image);

            // We don't want colors that are too dark.
            // If the color is too dark, make it brighter by reducing the range
            // and adding a constant color.
            int luminosity = GetColorApproximateLuminosity(color);
            if (luminosity < CutOffLuminosity)
            {
                color = System.Drawing.Color.FromArgb(
                    Math.Min(CutOffLuminosity + color.R, byte.MaxValue),
                    Math.Min(CutOffLuminosity + color.G, byte.MaxValue),
                    Math.Min(CutOffLuminosity + color.B, byte.MaxValue));
            }

            return color;
        }

        public static Color GetColor(Bitmap image)
        {
            var colors = new Color[TotalPixels];

            var dominantColorBin = new Dictionary<int, int>();

            int xStep = image.Width / PixelsPerAxis;
            int yStep = image.Height / PixelsPerAxis;

            int i = 0;

            for (int y = 0; y < image.Height; y += yStep)
            {
                for (int x = 0; x < image.Width; x += xStep)
                {
                    var col = image.GetPixel(x, y);

                    var qck = GetQuantizedColorKey(col);

                    if (dominantColorBin.ContainsKey(qck))
                    {
                        dominantColorBin[qck]++;
                    }
                    else
                    {
                        dominantColorBin.Add(qck, 1);
                    }

                    colors[i++] = col;
                }
            }

            int maxHitCount = dominantColorBin.Values.Max();

            return colors.OrderByDescending(x => GetColorScore(dominantColorBin, maxHitCount, x)).First();
        }

        private static int GetColorScore(Dictionary<int, int> dominantColorBin, int maxHitCount, Color color)
        {
            var qck = GetQuantizedColorKey(color);
            var hitCount = dominantColorBin[qck];
            var balancedHitCount = BalanceHitCount(hitCount, maxHitCount);
            var quantSat = (GetColorSaturation(color) >> SatQuantShift) << SatQuantBits;

            // Compute score from saturation and dominance of the color.
            // We prefer more vivid colors over dominant ones, so give more weight to the saturation.
            var score = quantSat + (quantSat >> 2) + balancedHitCount;

            // We avoid picking colors that are too dark by applying a penalty to the
            // score of those colors.
            int luminosity = GetColorApproximateLuminosity(color);
            if (luminosity < CutOffLuminosity)
            {
                score >>= CutOffPenaltyShift;
            }

            return score;
        }

        private static int BalanceHitCount(int hitCount, int maxHitCount)
        {
            return (hitCount << 8) / maxHitCount;
        }

        private static int GetColorApproximateLuminosity(Color color)
        {
            return (color.R + color.G + color.B) / 3;
        }

        private static int GetColorSaturation(Color color)
        {
            int cMax = Math.Max(Math.Max(color.R, color.G), color.B);

            if (cMax == 0)
            {
                return 0;
            }

            int cMin = Math.Min(Math.Min(color.R, color.G), color.B);
            int delta = cMax - cMin;
            return (delta << 8) / cMax;
        }

        private static int GetQuantizedColorKey(Color col)
        {
            return (col.R >> RgbQuantShift) |
                ((col.G >> RgbQuantShift) << RgbQuantBits) |
                ((col.B >> RgbQuantShift) << (RgbQuantBits * 2));
        }
    }
}
