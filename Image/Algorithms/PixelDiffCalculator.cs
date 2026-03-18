using System;
using System.Drawing;

namespace SimpleSeleniumSupport.Image.Algorithms
{
    /// <summary>
    /// Exact per-pixel comparison.
    /// Score = (1 - diffPixels / totalPixels) × 100.
    /// Fast but sensitive to sub-pixel rendering and anti-aliasing differences.
    /// </summary>
    internal sealed class PixelDiffCalculator : IImageComparator
    {
        /// <summary>Per-channel tolerance (0–255). Pixels within tolerance = identical.</summary>
        public int Tolerance { get; set; } = 0;

        public string AlgorithmName => "PixelDiff";

        public double Compare(Bitmap baseline, Bitmap actual)
        {
            using var pa = new AlgoPixelBuffer(baseline);
            using var pb = new AlgoPixelBuffer(actual);

            int w = pa.Width, h = pa.Height, total = w * h;
            if (total == 0) return 100.0;

            int diffCount = 0;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var (r1, g1, b1) = pa.GetRgb(x, y);
                    var (r2, g2, b2) = pb.GetRgb(x, y);
                    if (Math.Abs(r1 - r2) > Tolerance ||
                        Math.Abs(g1 - g2) > Tolerance ||
                        Math.Abs(b1 - b2) > Tolerance)
                        diffCount++;
                }

            return Math.Round((1.0 - (double)diffCount / total) * 100.0, 2);
        }
    }
}
