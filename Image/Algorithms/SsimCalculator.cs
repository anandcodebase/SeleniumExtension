using System;
using System.Drawing;

namespace SimpleSeleniumSupport.Image.Algorithms
{
    /// <summary>
    /// Luminance-based Structural Similarity Index (SSIM) comparator.
    /// See Wang et al. 2004 — "Image quality assessment: from error visibility to structural similarity".
    /// </summary>
    internal sealed class SsimCalculator : IImageComparator
    {
        // SSIM stability constants — prevent division by zero in uniform regions.
        // L  = 255 (8-bit dynamic range)
        // K1 = 0.01, K2 = 0.03 (Wang et al. 2004)
        // C1 = (K1*L)^2 = 2.55^2  = 6.5025
        // C2 = (K2*L)^2 = 7.65^2  = 58.5225
        private const double C1 = 6.5025;
        private const double C2 = 58.5225;

        public string AlgorithmName => "SSIM";

        public double Compare(Bitmap baseline, Bitmap actual)
        {
            using var pa = new AlgoPixelBuffer(baseline);
            using var pb = new AlgoPixelBuffer(actual);

            int w = pa.Width, h = pa.Height, n = w * h;
            if (n == 0) return 100.0;

            var lumA = new double[n];
            var lumB = new double[n];
            double sumA = 0, sumB = 0;

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i  = y * w + x;
                    double la = pa.Luminance(x, y);
                    double lb = pb.Luminance(x, y);
                    lumA[i] = la; lumB[i] = lb;
                    sumA += la;  sumB += lb;
                }

            double meanA = sumA / n, meanB = sumB / n;
            double varA = 0, varB = 0, cov = 0;

            for (int i = 0; i < n; i++)
            {
                double da = lumA[i] - meanA;
                double db = lumB[i] - meanB;
                varA += da * da; varB += db * db; cov += da * db;
            }
            varA /= (n - 1); varB /= (n - 1); cov /= (n - 1);

            double num = (2 * meanA * meanB + C1) * (2 * cov + C2);
            double den = (meanA * meanA + meanB * meanB + C1) * (varA + varB + C2);

            return Math.Round(Math.Clamp(num / den, 0, 1) * 100.0, 2);
        }
    }
}
