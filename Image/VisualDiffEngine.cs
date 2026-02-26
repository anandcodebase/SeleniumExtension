using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace SimpleSeleniumSupport.Image
{
    /// <summary>
    /// Offline, deterministic visual diff engine using luminance-based SSIM,
    /// Sobel edge detection, and pixel-level diff with anti-aliasing tolerance.
    /// </summary>
    public static class VisualDiffEngine
    {
        public sealed class Options
        {
            /// <summary>Regions to compare (if empty => whole image)</summary>
            public List<Rectangle> CompareRegions { get; } = new();

            /// <summary>Regions to ignore (timestamps, ads, animations)</summary>
            public List<Rectangle> IgnoreRegions { get; } = new();

            /// <summary>Weight of SSIM in final score (0..1)</summary>
            public double SsimWeight { get; set; } = 0.7;

            /// <summary>Weight of edge diff in final score (0..1)</summary>
            public double EdgeWeight { get; set; } = 0.3;

            /// <summary>Downscale longest edge to this size (0 = use original)</summary>
            public int NormalizeSize { get; set; } = 0;

            /// <summary>Generate heatmap image</summary>
            public bool GenerateHeatmap { get; set; } = true;

            /// <summary>Directory to save artifacts (null => temp)</summary>
            public string OutputDirectory { get; set; }

            /// <summary>Pixel radius for anti-aliasing neighbor check (0 = disabled)</summary>
            public int AntiAliasingTolerance { get; set; } = 0;

            /// <summary>Per-channel tolerance (0-255) below which pixels match</summary>
            public int PixelTolerance { get; set; } = 0;
        }

        public sealed class Result
        {
            public double SimilarityPercent { get; set; }
            public double SsimPercent { get; set; }
            public double EdgePercent { get; set; }
            public double PixelDiffPercent { get; set; }
            public int PixelDiffCount { get; set; }
            public int TotalPixels { get; set; }
            public string Reasoning { get; set; } = "";
            public string HeatmapPath { get; set; }
            public string DiffImagePath { get; set; }
        }

        // ===================== PIXEL BUFFER (FAST ACCESS) =====================

        /// <summary>
        /// Fast read-only pixel accessor using LockBits + Marshal.Copy.
        /// Replaces GetPixel calls for 10-50x speedup.
        /// </summary>
        private sealed class PixelBuffer : IDisposable
        {
            public readonly int Width;
            public readonly int Height;
            public readonly byte[] Pixels;
            public readonly int Stride;

            public PixelBuffer(Bitmap bmp)
            {
                Width = bmp.Width;
                Height = bmp.Height;
                var rect = new Rectangle(0, 0, Width, Height);
                var data = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
                Stride = data.Stride;
                Pixels = new byte[Stride * Height];
                Marshal.Copy(data.Scan0, Pixels, 0, Pixels.Length);
                bmp.UnlockBits(data);
            }

            /// <summary>BT.601 luminance: 0.299R + 0.587G + 0.114B</summary>
            public double Luminance(int x, int y)
            {
                int offset = y * Stride + x * 3;
                return 0.299 * Pixels[offset + 2] +
                       0.587 * Pixels[offset + 1] +
                       0.114 * Pixels[offset];
            }

            public (byte R, byte G, byte B) GetRgb(int x, int y)
            {
                int offset = y * Stride + x * 3;
                return (Pixels[offset + 2], Pixels[offset + 1], Pixels[offset]);
            }

            public void Dispose() { }
        }

        // ===================== PUBLIC ENTRY =====================

        public static Result Compare(byte[] expectedBytes, byte[] actualBytes, Options opt = null)
        {
            opt ??= new Options();

            var (expImg, expOrigSize) = LoadAndNormalize(expectedBytes, opt.NormalizeSize);
            var (actRaw, _) = LoadAndNormalize(actualBytes, opt.NormalizeSize);

            using var expDisposable = expImg;

            // Ensure same dimensions (resize actual to match baseline)
            Bitmap actImg = actRaw;
            bool actNeedsDispose = false;
            if (actRaw.Width != expImg.Width || actRaw.Height != expImg.Height)
            {
                actImg = new Bitmap(actRaw, new Size(expImg.Width, expImg.Height));
                actNeedsDispose = true;
                actRaw.Dispose();
            }

            try
            {
                // Scale regions from original image coordinates to normalized coordinates
                var scaledCompareRegions = ScaleRegions(
                    opt.CompareRegions, expOrigSize,
                    new Size(expImg.Width, expImg.Height));
                var scaledIgnoreRegions = ScaleRegions(
                    opt.IgnoreRegions, expOrigSize,
                    new Size(expImg.Width, expImg.Height));

                ApplyMask(expImg, scaledIgnoreRegions);
                ApplyMask(actImg, scaledIgnoreRegions);

                double ssimScore;
                double edgeScore;

                if (scaledCompareRegions.Any())
                {
                    var ssimList = new List<double>();
                    var edgeList = new List<double>();

                    foreach (var r in scaledCompareRegions)
                    {
                        using var a = Crop(expImg, r);
                        using var b = Crop(actImg, r);

                        ssimList.Add(ComputeSSIM(a, b) * 100.0);
                        edgeList.Add(ComputeEdgeSimilarity(a, b));
                    }

                    ssimScore = ssimList.Average();
                    edgeScore = edgeList.Average();
                }
                else
                {
                    ssimScore = ComputeSSIM(expImg, actImg) * 100.0;
                    edgeScore = ComputeEdgeSimilarity(expImg, actImg);
                }

                // Pixel diff with anti-aliasing tolerance
                var (diffCount, totalPixels) = ComputePixelDiff(
                    expImg, actImg, opt.PixelTolerance, opt.AntiAliasingTolerance);
                double pixelDiffPercent = totalPixels > 0
                    ? Math.Round((double)diffCount / totalPixels * 100.0, 2)
                    : 0;

                double finalScore = Math.Round(
                    (ssimScore * opt.SsimWeight) + (edgeScore * opt.EdgeWeight), 2);

                var result = new Result
                {
                    SimilarityPercent = finalScore,
                    SsimPercent = Math.Round(ssimScore, 2),
                    EdgePercent = Math.Round(edgeScore, 2),
                    PixelDiffPercent = pixelDiffPercent,
                    PixelDiffCount = diffCount,
                    TotalPixels = totalPixels,
                    Reasoning =
                        $"SSIM {opt.SsimWeight:P0}, Edge {opt.EdgeWeight:P0}. " +
                        $"Pixel diff: {diffCount}/{totalPixels} ({pixelDiffPercent}%). " +
                        $"Ignored {opt.IgnoreRegions.Count} region(s)."
                };

                if (opt.GenerateHeatmap)
                    result.HeatmapPath = GenerateHeatmap(expImg, actImg, opt.OutputDirectory);

                result.DiffImagePath = GenerateSideBySide(
                    expImg, actImg, result.HeatmapPath, opt.OutputDirectory);

                return result;
            }
            finally
            {
                if (actNeedsDispose) actImg.Dispose();
                else actRaw.Dispose();
            }
        }

        // ===================== LOAD & NORMALIZE =====================

        private static (Bitmap normalized, Size originalSize) LoadAndNormalize(byte[] bytes, int maxEdge)
        {
            using var ms = new MemoryStream(bytes);
            using var original = System.Drawing.Image.FromStream(ms);
            var origSize = original.Size;

            if (maxEdge <= 0)
                return (new Bitmap(original), origSize);

            // Scale so longest edge == maxEdge, preserving aspect ratio
            double scale = (double)maxEdge / Math.Max(original.Width, original.Height);
            int newW = Math.Max(1, (int)(original.Width * scale));
            int newH = Math.Max(1, (int)(original.Height * scale));

            return (new Bitmap(original, new Size(newW, newH)), origSize);
        }

        private static Bitmap Crop(Bitmap src, Rectangle r)
        {
            var safe = Rectangle.Intersect(
                new Rectangle(0, 0, src.Width, src.Height), r);
            if (safe.Width <= 0 || safe.Height <= 0)
                return new Bitmap(1, 1, PixelFormat.Format24bppRgb);
            return src.Clone(safe, PixelFormat.Format24bppRgb);
        }

        private static void ApplyMask(Bitmap bmp, IEnumerable<Rectangle> masks)
        {
            if (masks == null) return;
            using var g = Graphics.FromImage(bmp);
            foreach (var r in masks)
                g.FillRectangle(Brushes.Black, r);
        }

        // ===================== REGION SCALING =====================

        private static List<Rectangle> ScaleRegions(
            List<Rectangle> regions, Size originalSize, Size normalizedSize)
        {
            if (regions.Count == 0) return regions;
            if (originalSize == normalizedSize) return regions;

            double sx = (double)normalizedSize.Width / originalSize.Width;
            double sy = (double)normalizedSize.Height / originalSize.Height;

            return regions.Select(r => new Rectangle(
                (int)(r.X * sx),
                (int)(r.Y * sy),
                Math.Max(1, (int)(r.Width * sx)),
                Math.Max(1, (int)(r.Height * sy))
            )).ToList();
        }

        // ===================== SSIM (LUMINANCE) =====================

        private static double ComputeSSIM(Bitmap a, Bitmap b)
        {
            // SSIM stability constants — prevent division by zero in uniform regions.
            // Derived from the standard SSIM formula: C = (K * L)^2
            //   L  = 255   (dynamic range of an 8-bit channel)
            //   K1 = 0.01  (luminance stability factor, Wang et al. 2004)
            //   K2 = 0.03  (contrast/structure stability factor, Wang et al. 2004)
            // C1 = (K1 * L)^2 = (0.01 * 255)^2 = 2.55^2  = 6.5025
            // C2 = (K2 * L)^2 = (0.03 * 255)^2 = 7.65^2  = 58.5225
            const double C1 = 6.5025;
            const double C2 = 58.5225;

            using var pa = new PixelBuffer(a);
            using var pb = new PixelBuffer(b);

            int w = pa.Width, h = pa.Height;
            int n = w * h;
            if (n == 0) return 1.0;

            // Single pass to build luminance arrays
            var lumA = new double[n];
            var lumB = new double[n];
            double sumA = 0, sumB = 0;

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    int i = y * w + x;
                    double la = pa.Luminance(x, y);
                    double lb = pb.Luminance(x, y);
                    lumA[i] = la;
                    lumB[i] = lb;
                    sumA += la;
                    sumB += lb;
                }

            double meanA = sumA / n;
            double meanB = sumB / n;

            double varA = 0, varB = 0, cov = 0;
            for (int i = 0; i < n; i++)
            {
                double da = lumA[i] - meanA;
                double db = lumB[i] - meanB;
                varA += da * da;
                varB += db * db;
                cov += da * db;
            }

            varA /= (n - 1);
            varB /= (n - 1);
            cov /= (n - 1);

            double num = (2 * meanA * meanB + C1) * (2 * cov + C2);
            double den = (meanA * meanA + meanB * meanB + C1) * (varA + varB + C2);

            return Math.Clamp(num / den, 0, 1);
        }

        // ===================== EDGE MAP (LUMINANCE) =====================

        private static double ComputeEdgeSimilarity(Bitmap a, Bitmap b)
        {
            using var ea = ExtractEdges(a);
            using var eb = ExtractEdges(b);

            using var pa = new PixelBuffer(ea);
            using var pb = new PixelBuffer(eb);

            double diff = 0;
            for (int y = 0; y < pa.Height; y++)
                for (int x = 0; x < pa.Width; x++)
                    diff += Math.Abs(pa.GetRgb(x, y).R - pb.GetRgb(x, y).R);

            double max = pa.Width * pa.Height * 255.0;
            return max > 0 ? Math.Round((1.0 - diff / max) * 100.0, 2) : 100.0;
        }

        private static Bitmap ExtractEdges(Bitmap src)
        {
            int w = src.Width, h = src.Height;
            var edges = new Bitmap(w, h, PixelFormat.Format24bppRgb);

            int[,] gx =
            {
                { -1, 0, 1 },
                { -2, 0, 2 },
                { -1, 0, 1 }
            };

            int[,] gy =
            {
                { 1, 2, 1 },
                { 0, 0, 0 },
                { -1, -2, -1 }
            };

            using var pb = new PixelBuffer(src);

            // Build luminance grid
            var lum = new double[h, w];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    lum[y, x] = pb.Luminance(x, y);

            // Lock output for writing
            var rect = new Rectangle(0, 0, w, h);
            var data = edges.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            var outPixels = new byte[data.Stride * h];

            for (int y = 1; y < h - 1; y++)
                for (int x = 1; x < w - 1; x++)
                {
                    double sx = 0, sy = 0;

                    for (int ky = -1; ky <= 1; ky++)
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            double g = lum[y + ky, x + kx];
                            sx += gx[ky + 1, kx + 1] * g;
                            sy += gy[ky + 1, kx + 1] * g;
                        }

                    int mag = Math.Clamp((int)Math.Sqrt(sx * sx + sy * sy), 0, 255);
                    int offset = y * data.Stride + x * 3;
                    outPixels[offset] = (byte)mag;     // B
                    outPixels[offset + 1] = (byte)mag; // G
                    outPixels[offset + 2] = (byte)mag; // R
                }

            Marshal.Copy(outPixels, 0, data.Scan0, outPixels.Length);
            edges.UnlockBits(data);

            return edges;
        }

        // ===================== PIXEL DIFF =====================

        private static (int diffCount, int totalPixels) ComputePixelDiff(
            Bitmap a, Bitmap b, int pixelTolerance, int aaRadius)
        {
            using var pa = new PixelBuffer(a);
            using var pb = new PixelBuffer(b);

            int w = pa.Width, h = pa.Height;
            int diffCount = 0;

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var (r1, g1, b1) = pa.GetRgb(x, y);
                    var (r2, g2, b2) = pb.GetRgb(x, y);

                    if (Math.Abs(r1 - r2) <= pixelTolerance &&
                        Math.Abs(g1 - g2) <= pixelTolerance &&
                        Math.Abs(b1 - b2) <= pixelTolerance)
                        continue;

                    // Anti-aliasing check: see if a neighbor in B matches this pixel in A
                    if (aaRadius > 0 &&
                        HasSimilarNeighbor(pb, x, y, r1, g1, b1, pixelTolerance, aaRadius))
                        continue;

                    diffCount++;
                }

            return (diffCount, w * h);
        }

        private static bool HasSimilarNeighbor(
            PixelBuffer buf, int cx, int cy,
            byte tr, byte tg, byte tb,
            int tolerance, int radius)
        {
            int xMin = Math.Max(0, cx - radius);
            int xMax = Math.Min(buf.Width - 1, cx + radius);
            int yMin = Math.Max(0, cy - radius);
            int yMax = Math.Min(buf.Height - 1, cy + radius);

            for (int ny = yMin; ny <= yMax; ny++)
                for (int nx = xMin; nx <= xMax; nx++)
                {
                    if (nx == cx && ny == cy) continue;
                    var (r, g, b) = buf.GetRgb(nx, ny);
                    if (Math.Abs(r - tr) <= tolerance &&
                        Math.Abs(g - tg) <= tolerance &&
                        Math.Abs(b - tb) <= tolerance)
                        return true;
                }

            return false;
        }

        // ===================== HEATMAP (FULL COLOR) =====================

        private static string GenerateHeatmap(Bitmap a, Bitmap b, string outputDir)
        {
            // Maximum possible Euclidean distance in RGB space:
            //   sqrt(255^2 + 255^2 + 255^2) = sqrt(195075) ≈ 441.6729...
            // Dividing the per-pixel distance by this value normalises it to [0, 1].
            const double MaxRgbDistance = 441.67;

            using var pa = new PixelBuffer(a);
            using var pb = new PixelBuffer(b);

            int w = pa.Width, h = pa.Height;
            var heat = new Bitmap(w, h, PixelFormat.Format24bppRgb);

            var rect = new Rectangle(0, 0, w, h);
            var data = heat.LockBits(rect, ImageLockMode.WriteOnly, PixelFormat.Format24bppRgb);
            var outPixels = new byte[data.Stride * h];

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    var (r1, g1, b1) = pa.GetRgb(x, y);
                    var (r2, g2, b2) = pb.GetRgb(x, y);

                    // Euclidean color distance normalized to [0, 1]
                    double dist = Math.Sqrt(
                        (r1 - r2) * (r1 - r2) +
                        (g1 - g2) * (g1 - g2) +
                        (b1 - b2) * (b1 - b2)) / MaxRgbDistance;

                    byte hr, hg, hb;
                    if (dist < 0.01)
                    {
                        // No difference: dark green
                        hr = 0; hg = 40; hb = 0;
                    }
                    else if (dist < 0.5)
                    {
                        // Green to Yellow
                        double t = dist / 0.5;
                        hr = (byte)(255 * t);
                        hg = 255;
                        hb = 0;
                    }
                    else
                    {
                        // Yellow to Red
                        double t = Math.Min(1.0, (dist - 0.5) / 0.5);
                        hr = 255;
                        hg = (byte)(255 * (1 - t));
                        hb = 0;
                    }

                    int offset = y * data.Stride + x * 3;
                    outPixels[offset] = hb;     // B
                    outPixels[offset + 1] = hg; // G
                    outPixels[offset + 2] = hr; // R
                }

            Marshal.Copy(outPixels, 0, data.Scan0, outPixels.Length);
            heat.UnlockBits(data);

            outputDir ??= Path.GetTempPath();
            Directory.CreateDirectory(outputDir);

            string path = Path.Combine(outputDir,
                $"visual_diff_heatmap_{DateTime.Now:yyyyMMdd_HHmmssfff}.png");
            heat.Save(path, ImageFormat.Png);
            heat.Dispose();
            return path;
        }

        // ===================== SIDE-BY-SIDE COMPOSITE =====================

        private static string GenerateSideBySide(
            Bitmap baseline, Bitmap actual, string heatmapPath, string outputDir)
        {
            Bitmap heatmap = null;
            try
            {
                if (!string.IsNullOrEmpty(heatmapPath) && File.Exists(heatmapPath))
                    heatmap = new Bitmap(heatmapPath);

                int panels = heatmap != null ? 3 : 2;
                int gap = 4;
                int totalW = baseline.Width * panels + gap * (panels - 1);
                int totalH = baseline.Height;

                using var composite = new Bitmap(totalW, totalH, PixelFormat.Format24bppRgb);
                using var g = Graphics.FromImage(composite);
                g.Clear(Color.White);

                g.DrawImage(baseline, 0, 0, baseline.Width, baseline.Height);
                g.DrawImage(actual, baseline.Width + gap, 0, actual.Width, actual.Height);
                if (heatmap != null)
                    g.DrawImage(heatmap, (baseline.Width + gap) * 2, 0,
                        heatmap.Width, heatmap.Height);

                outputDir ??= Path.GetTempPath();
                Directory.CreateDirectory(outputDir);
                string path = Path.Combine(outputDir,
                    $"visual_diff_composite_{DateTime.Now:yyyyMMdd_HHmmssfff}.png");
                composite.Save(path, ImageFormat.Png);
                return path;
            }
            finally
            {
                heatmap?.Dispose();
            }
        }
    }
}
