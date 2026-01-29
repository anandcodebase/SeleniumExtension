using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

namespace SimpleSeleniumSupport.VisualDiff
{
    /// <summary>
    /// Offline, deterministic visual diff engine (Playwright-style fallback).
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

            /// <summary>Downscale size for perceptual comparison</summary>
            public int NormalizeSize { get; set; } = 128;

            /// <summary>Generate heatmap image</summary>
            public bool GenerateHeatmap { get; set; } = true;

            /// <summary>Directory to save heatmap (null => temp)</summary>
            public string OutputDirectory { get; set; }
        }

        public sealed class Result
        {
            public double SimilarityPercent { get; set; }
            public double SsimPercent { get; set; }
            public double EdgePercent { get; set; }
            public string Reasoning { get; set; } = "";
            public string HeatmapPath { get; set; }
        }

        // ===================== PUBLIC ENTRY =====================

        public static Result Compare(byte[] expectedBytes, byte[] actualBytes, Options opt = null)
        {
            opt ??= new Options();

            using var expImg = LoadAndNormalize(expectedBytes, opt.NormalizeSize);
            using var actImg = LoadAndNormalize(actualBytes, opt.NormalizeSize);

            ApplyMask(expImg, opt.IgnoreRegions);
            ApplyMask(actImg, opt.IgnoreRegions);

            double ssimScore;
            double edgeScore;

            if (opt.CompareRegions.Any())
            {
                var ssimList = new List<double>();
                var edgeList = new List<double>();

                foreach (var r in opt.CompareRegions)
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

            double finalScore =
                (ssimScore * opt.SsimWeight) +
                (edgeScore * opt.EdgeWeight);

            var result = new Result
            {
                SimilarityPercent = Math.Round(finalScore, 2),
                SsimPercent = Math.Round(ssimScore, 2),
                EdgePercent = Math.Round(edgeScore, 2),
                Reasoning =
                    $"Fallback visual diff: SSIM {opt.SsimWeight:P0}, Edge {opt.EdgeWeight:P0}. " +
                    $"Ignored {opt.IgnoreRegions.Count} regions."
            };

            if (opt.GenerateHeatmap)
                result.HeatmapPath = GenerateHeatmap(expImg, actImg, opt.OutputDirectory);

            return result;
        }

        // ===================== CORE STEPS =====================

        private static Bitmap LoadAndNormalize(byte[] bytes, int size)
        {
            using var ms = new MemoryStream(bytes);
            using var img = System.Drawing.Image.FromStream(ms);
            return new Bitmap(img, new Size(size, size));
        }

        private static Bitmap Crop(Bitmap src, Rectangle r)
        {
            var safe = Rectangle.Intersect(
                new Rectangle(0, 0, src.Width, src.Height), r);
            return src.Clone(safe, PixelFormat.Format24bppRgb);
        }

        private static void ApplyMask(Bitmap bmp, IEnumerable<Rectangle> masks)
        {
            if (masks == null) return;
            using var g = Graphics.FromImage(bmp);
            foreach (var r in masks)
                g.FillRectangle(Brushes.Black, r);
        }

        // ===================== SSIM =====================

        private static double ComputeSSIM(Bitmap a, Bitmap b)
        {
            const double C1 = 6.5025;
            const double C2 = 58.5225;

            int w = a.Width, h = a.Height;
            int n = w * h;

            double meanA = 0, meanB = 0;

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    meanA += a.GetPixel(x, y).R;
                    meanB += b.GetPixel(x, y).R;
                }

            meanA /= n;
            meanB /= n;

            double varA = 0, varB = 0, cov = 0;

            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    double da = a.GetPixel(x, y).R - meanA;
                    double db = b.GetPixel(x, y).R - meanB;
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

        // ===================== EDGE MAP =====================

        private static double ComputeEdgeSimilarity(Bitmap a, Bitmap b)
        {
            using var ea = ExtractEdges(a);
            using var eb = ExtractEdges(b);

            double diff = 0;
            for (int y = 0; y < ea.Height; y++)
                for (int x = 0; x < ea.Width; x++)
                    diff += Math.Abs(ea.GetPixel(x, y).R - eb.GetPixel(x, y).R);

            double max = ea.Width * ea.Height * 255.0;
            return Math.Round((1.0 - diff / max) * 100.0, 2);
        }

        private static Bitmap ExtractEdges(Bitmap src)
        {
            var edges = new Bitmap(src.Width, src.Height);

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

            for (int y = 1; y < src.Height - 1; y++)
                for (int x = 1; x < src.Width - 1; x++)
                {
                    int sx = 0, sy = 0;

                    for (int ky = -1; ky <= 1; ky++)
                        for (int kx = -1; kx <= 1; kx++)
                        {
                            int g = src.GetPixel(x + kx, y + ky).R;
                            sx += gx[ky + 1, kx + 1] * g;
                            sy += gy[ky + 1, kx + 1] * g;
                        }

                    int mag = Math.Clamp((int)Math.Sqrt(sx * sx + sy * sy), 0, 255);
                    edges.SetPixel(x, y, Color.FromArgb(mag, mag, mag));
                }

            return edges;
        }

        // ===================== HEATMAP =====================

        private static string GenerateHeatmap(Bitmap a, Bitmap b, string outputDir)
        {
            var heat = new Bitmap(a.Width, a.Height);

            for (int y = 0; y < a.Height; y++)
                for (int x = 0; x < a.Width; x++)
                {
                    int d = Math.Abs(a.GetPixel(x, y).R - b.GetPixel(x, y).R);
                    heat.SetPixel(x, y, Color.FromArgb(d, 255, 0, 0));
                }

            outputDir ??= Path.GetTempPath();
            Directory.CreateDirectory(outputDir);

            string path = Path.Combine(
                outputDir,
                $"visual_diff_heatmap_{DateTime.Now:yyyyMMdd_HHmmssfff}.png");

            heat.Save(path, ImageFormat.Png);
            heat.Dispose();

            return path;
        }
    }
}
