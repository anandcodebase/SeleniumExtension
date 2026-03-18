using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace SimpleSeleniumSupport.Image.Algorithms
{
    /// <summary>
    /// DCT-based perceptual hash (pHash) comparator.
    /// Robust to minor scaling, compression artefacts, and brightness shifts.
    /// Similarity = (1 - HammingDistance/64) × 100.
    /// </summary>
    internal sealed class PerceptualHashCalculator : IImageComparator
    {
        private const int ResizeTarget = 32; // resize before DCT
        private const int DctKeep      = 8;  // use top-left 8×8 of DCT → 64-bit hash

        public string AlgorithmName => "pHash";

        public double Compare(Bitmap baseline, Bitmap actual)
        {
            ulong hashA = ComputeHash(baseline);
            ulong hashB = ComputeHash(actual);
            int hamming = HammingDistance(hashA, hashB);
            // 63 bits used (DC component [0,0] skipped); max Hamming distance = 63
            return Math.Round((1.0 - hamming / 63.0) * 100.0, 2);
        }

        // ── Hash computation ──────────────────────────────────────────────────

        private static ulong ComputeHash(Bitmap src)
        {
            using var small = new Bitmap(src, new Size(ResizeTarget, ResizeTarget));
            double[,] grey  = ToGreyscale(small);
            double[,] dct   = ComputeDct2D(grey);

            // Collect DctKeep×DctKeep coefficients, skipping DC [0,0]
            int count = DctKeep * DctKeep - 1;
            var coeffs = new double[count];
            int idx = 0; double sum = 0;

            for (int y = 0; y < DctKeep; y++)
                for (int x = 0; x < DctKeep; x++)
                {
                    if (x == 0 && y == 0) continue;
                    coeffs[idx++] = dct[y, x];
                    sum += dct[y, x];
                }

            double mean = sum / count;
            ulong hash = 0UL;
            for (int i = 0; i < 63; i++)
                if (coeffs[i] > mean)
                    hash |= 1UL << i;

            return hash;
        }

        private static double[,] ToGreyscale(Bitmap bmp)
        {
            int w = bmp.Width, h = bmp.Height;
            var data   = bmp.LockBits(new Rectangle(0, 0, w, h),
                ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            var pixels = new byte[data.Stride * h];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
            bmp.UnlockBits(data);

            var grey = new double[h, w];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int o = y * data.Stride + x * 3;
                    grey[y, x] = 0.299 * pixels[o + 2]
                               + 0.587 * pixels[o + 1]
                               + 0.114 * pixels[o];
                }
            return grey;
        }

        private static double[,] ComputeDct2D(double[,] input)
        {
            int n = input.GetLength(0);
            var rowDct = new double[n, n];
            for (int y = 0; y < n; y++)
            {
                var row = new double[n];
                for (int x = 0; x < n; x++) row[x] = input[y, x];
                var d = Dct1D(row);
                for (int x = 0; x < n; x++) rowDct[y, x] = d[x];
            }
            var result = new double[n, n];
            for (int x = 0; x < n; x++)
            {
                var col = new double[n];
                for (int y = 0; y < n; y++) col[y] = rowDct[y, x];
                var d = Dct1D(col);
                for (int y = 0; y < n; y++) result[y, x] = d[y];
            }
            return result;
        }

        private static double[] Dct1D(double[] input)
        {
            int n    = input.Length;
            var out_ = new double[n];
            double f = Math.PI / (2.0 * n);
            for (int k = 0; k < n; k++)
            {
                double sum = 0;
                for (int i = 0; i < n; i++)
                    sum += input[i] * Math.Cos((2 * i + 1) * k * f);
                double scale = k == 0 ? Math.Sqrt(1.0 / n) : Math.Sqrt(2.0 / n);
                out_[k] = scale * sum;
            }
            return out_;
        }

        private static int HammingDistance(ulong a, ulong b)
        {
            ulong xor = a ^ b;
            int count = 0;
            while (xor != 0) { count += (int)(xor & 1); xor >>= 1; }
            return count;
        }
    }
}
