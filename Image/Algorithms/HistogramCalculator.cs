using System;
using System.Drawing;

namespace SimpleSeleniumSupport.Image.Algorithms
{
    /// <summary>
    /// Greyscale histogram correlation comparator.
    /// Uses Pearson correlation of two 256-bin histograms mapped to [0, 100].
    /// Tolerant of local layout shifts and lighting changes.
    /// </summary>
    internal sealed class HistogramCalculator : IImageComparator
    {
        public string AlgorithmName => "Histogram";

        public double Compare(Bitmap baseline, Bitmap actual)
        {
            var histA = BuildHistogram(baseline);
            var histB = BuildHistogram(actual);
            double corr = PearsonCorrelation(histA, histB);
            // Map [-1, 1] correlation to [0, 100]
            return Math.Round(Math.Clamp((corr + 1.0) / 2.0 * 100.0, 0, 100), 2);
        }

        private static double[] BuildHistogram(Bitmap bmp)
        {
            using var buf  = new AlgoPixelBuffer(bmp);
            var hist = new double[256];
            for (int y = 0; y < buf.Height; y++)
                for (int x = 0; x < buf.Width; x++)
                {
                    int lum = (int)Math.Clamp(buf.Luminance(x, y), 0, 255);
                    hist[lum]++;
                }
            return hist;
        }

        private static double PearsonCorrelation(double[] a, double[] b)
        {
            int n = a.Length;
            double mA = 0, mB = 0;
            for (int i = 0; i < n; i++) { mA += a[i]; mB += b[i]; }
            mA /= n; mB /= n;

            double num = 0, dA = 0, dB = 0;
            for (int i = 0; i < n; i++)
            {
                double da = a[i] - mA, db = b[i] - mB;
                num += da * db; dA += da * da; dB += db * db;
            }
            double den = Math.Sqrt(dA * dB);
            return den < 1e-10 ? 1.0 : num / den;
        }
    }
}
