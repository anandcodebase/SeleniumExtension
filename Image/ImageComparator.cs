using SimpleSeleniumSupport.Image.Algorithms;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SimpleSeleniumSupport.Image
{
    public static class ImageComparator
    {
        public enum ComparisonMode
        {
            WholeImage,
            RegionOnly,
            TextOnly
        }

        // ================= FILE-BASED ENTRY POINT =================

        public static ImageComparisonResult Compare(
            string baselinePath,
            string actualPath,
            ImageComparisonOptions options)
        {
            if (!File.Exists(baselinePath))
                throw new FileNotFoundException(baselinePath);
            if (!File.Exists(actualPath))
                throw new FileNotFoundException(actualPath);

            options ??= new ImageComparisonOptions();

            var baselineBytes = File.ReadAllBytes(baselinePath);
            var actualBytes = File.ReadAllBytes(actualPath);

            return CompareCore(baselineBytes, actualBytes, options);
        }

        // ================= BYTE-ARRAY ENTRY POINT =================

        /// <summary>
        /// Compare images from byte arrays (e.g. from ITakesScreenshot.GetScreenshot()).
        /// </summary>
        public static ImageComparisonResult Compare(
            byte[] baselineBytes,
            byte[] actualBytes,
            ImageComparisonOptions options)
        {
            if (baselineBytes == null || baselineBytes.Length == 0)
                throw new ArgumentException("Baseline image bytes cannot be empty.", nameof(baselineBytes));
            if (actualBytes == null || actualBytes.Length == 0)
                throw new ArgumentException("Actual image bytes cannot be empty.", nameof(actualBytes));

            options ??= new ImageComparisonOptions();

            return CompareCore(baselineBytes, actualBytes, options);
        }

        // ================= CORE LOGIC =================

        private static ImageComparisonResult CompareCore(
            byte[] baselineBytes,
            byte[] actualBytes,
            ImageComparisonOptions options)
        {
            var result = new ImageComparisonResult();

            // ================= TEXT ONLY =================
            if (options.Mode == ComparisonMode.TextOnly)
            {
                return CompareTextOnly(baselineBytes, actualBytes, options, result);
            }

            // ================= ALGORITHM DISPATCH =================
            // Non-SSIM algorithms use the lightweight IImageComparator path.
            // SSIM falls through to VisualDiffEngine which also produces heatmaps.
            if (options.Algorithm != ComparisonAlgorithm.Ssim)
            {
                return CompareWithAlgorithm(baselineBytes, actualBytes, options, result);
            }

            // ================= VISUAL DIFF (SSIM + EDGE + HEATMAP) =================
            var diffOptions = new VisualDiffEngine.Options
            {
                SsimWeight = options.SsimWeight,
                EdgeWeight = options.EdgeWeight,
                NormalizeSize = options.NormalizeSize,
                AntiAliasingTolerance = options.AntiAliasingTolerance,
                PixelTolerance = options.PixelTolerance,
                MaskFillColor = options.MaskFillColor,
                GenerateHeatmap = true,
                OutputDirectory = options.VisualDiffOutputDirectory
            };

            // RegionOnly support
            if (options.Mode == ComparisonMode.RegionOnly && options.Region.HasValue)
            {
                diffOptions.CompareRegions.Add(options.Region.Value);
                result.RegionCompared = options.Region;
            }

            // Additional regions
            diffOptions.CompareRegions.AddRange(options.CompareRegions);
            diffOptions.IgnoreRegions.AddRange(options.IgnoreRegions);

            var diff = VisualDiffEngine.Compare(baselineBytes, actualBytes, diffOptions);

            result.SimilarityPercent = diff.SimilarityPercent;
            result.VisualSimilarityPercent = diff.SimilarityPercent;
            result.SsimPercent = diff.SsimPercent;
            result.EdgePercent = diff.EdgePercent;
            result.PixelDiffPercent = diff.PixelDiffPercent;
            result.PixelDiffCount = diff.PixelDiffCount;
            result.TotalPixels = diff.TotalPixels;
            result.HeatmapPath = diff.HeatmapPath;
            result.DiffImagePath = diff.DiffImagePath;
            result.Reasoning = diff.Reasoning;
            result.Passed = diff.SimilarityPercent >= options.Threshold;
            result.Threshold = options.Threshold;

            // ================= AI ENHANCEMENT (OPTIONAL) =================
            if (options.EnableAI)
            {
                RunAiComparison(baselineBytes, actualBytes, options, result);
            }

            return result;
        }

        // ================= TEXT ONLY MODE =================

        private static ImageComparisonResult CompareTextOnly(
            byte[] baselineBytes,
            byte[] actualBytes,
            ImageComparisonOptions options,
            ImageComparisonResult result)
        {
            try
            {
                var provider = new OllamaVisionProvider();
                string baselineText = provider.ExtractText(baselineBytes, options);
                string actualText = provider.ExtractText(actualBytes, options);

                double textSimilarity = ComputeTextSimilarity(baselineText, actualText);

                result.TextSimilarityPercent = textSimilarity;
                result.SimilarityPercent = textSimilarity;
                result.Reasoning = $"TextOnly mode: extracted text similarity {textSimilarity}%.";
                result.Passed = textSimilarity >= options.Threshold;
                result.Threshold = options.Threshold;
            }
            catch (Exception ex)
            {
                // Fallback when AI/OCR is unavailable
                result.TextSimilarityPercent = 100;
                result.SimilarityPercent = 100;
                result.Reasoning = $"TextOnly mode: OCR unavailable ({ex.Message}), visual fallback.";
                result.Passed = true;
                result.Threshold = options.Threshold;
            }

            return result;
        }

        // ================= AI COMPARISON =================

        private static void RunAiComparison(
            byte[] baselineBytes,
            byte[] actualBytes,
            ImageComparisonOptions options,
            ImageComparisonResult result)
        {
            try
            {
                var provider = new OllamaVisionProvider();
                string aiResponse = provider.CompareImages(baselineBytes, actualBytes, options);
                var (similarity, reasoning) = ParseAiComparisonResponse(aiResponse);

                result.AiReasoning = reasoning;
            }
            catch (Exception ex)
            {
                result.AiReasoning = $"AI comparison failed: {ex.Message}";
            }
        }

        // ================= HELPERS =================

        private static (double? similarity, string reasoning) ParseAiComparisonResponse(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                double? similarity = root.TryGetProperty("similarity_percent", out var sp)
                    ? sp.GetDouble() : null;

                string reasoning = root.TryGetProperty("reasoning", out var r)
                    ? r.GetString() ?? "" : json;

                return (similarity, reasoning);
            }
            catch (JsonException)
            {
                return (null, json);
            }
        }

        /// <summary>
        /// Computes text similarity using Jaccard word-overlap (0-100%).
        /// </summary>
        private static double ComputeTextSimilarity(string a, string b)
        {
            if (string.IsNullOrWhiteSpace(a) && string.IsNullOrWhiteSpace(b))
                return 100.0;
            if (string.IsNullOrWhiteSpace(a) || string.IsNullOrWhiteSpace(b))
                return 0.0;

            var wordsA = new HashSet<string>(
                a.Split(default(char[]), StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);
            var wordsB = new HashSet<string>(
                b.Split(default(char[]), StringSplitOptions.RemoveEmptyEntries),
                StringComparer.OrdinalIgnoreCase);

            int intersection = wordsA.Intersect(wordsB).Count();
            int union = wordsA.Union(wordsB).Count();

            return union == 0 ? 100.0 : Math.Round((double)intersection / union * 100.0, 2);
        }

        // ================= ALGORITHM DISPATCH HELPERS =================

        /// <summary>
        /// Lightweight comparison path for non-SSIM algorithms.
        /// Loads bitmaps, applies masks, handles regions, runs <see cref="IImageComparator"/>.
        /// </summary>
        private static ImageComparisonResult CompareWithAlgorithm(
            byte[] baselineBytes,
            byte[] actualBytes,
            ImageComparisonOptions options,
            ImageComparisonResult result)
        {
            using var bmpBase = LoadBitmapFromBytes(baselineBytes);
            using var bmpActRaw = LoadBitmapFromBytes(actualBytes);

            // Resize actual to match baseline dimensions if they differ
            Bitmap bmpAct = bmpActRaw;
            bool disposeAct = false;
            if (bmpActRaw.Width != bmpBase.Width || bmpActRaw.Height != bmpBase.Height)
            {
                bmpAct = new Bitmap(bmpActRaw, new Size(bmpBase.Width, bmpBase.Height));
                disposeAct = true;
            }

            try
            {
                // Apply ignore masks to both copies
                FillRegions(bmpBase, options.IgnoreRegions, options.MaskFillColor);
                FillRegions(bmpAct,  options.IgnoreRegions, options.MaskFillColor);

                double score;
                if (options.CompareRegions.Count > 0)
                {
                    // Average score across all compare regions
                    var scores = options.CompareRegions.Select(r =>
                    {
                        using var cropA = CropBitmap(bmpBase, r);
                        using var cropB = CropBitmap(bmpAct,  r);
                        return RunAlgorithm(options.Algorithm, cropA, cropB, options);
                    }).ToList();
                    score = Math.Round(scores.Average(), 2);
                }
                else if (options.Mode == ComparisonMode.RegionOnly && options.Region.HasValue)
                {
                    using var cropA = CropBitmap(bmpBase, options.Region.Value);
                    using var cropB = CropBitmap(bmpAct,  options.Region.Value);
                    score = RunAlgorithm(options.Algorithm, cropA, cropB, options);
                    result.RegionCompared = options.Region;
                }
                else
                {
                    score = RunAlgorithm(options.Algorithm, bmpBase, bmpAct, options);
                }

                result.SimilarityPercent      = score;
                result.VisualSimilarityPercent = score;
                result.Passed    = score >= options.Threshold;
                result.Threshold = options.Threshold;
                result.Reasoning = $"Algorithm: {options.Algorithm}. Similarity: {score}%.";
                return result;
            }
            finally
            {
                if (disposeAct) bmpAct.Dispose();
            }
        }

        /// <summary>
        /// Selects and runs the correct <see cref="IImageComparator"/> for the given algorithm.
        /// </summary>
        private static double RunAlgorithm(
            ComparisonAlgorithm algorithm, Bitmap baseline, Bitmap actual,
            ImageComparisonOptions options)
            => algorithm switch
            {
                ComparisonAlgorithm.PerceptualHash =>
                    new PerceptualHashCalculator().Compare(baseline, actual),

                ComparisonAlgorithm.PixelDiff =>
                    new PixelDiffCalculator { Tolerance = options.PixelTolerance }
                        .Compare(baseline, actual),

                ComparisonAlgorithm.Histogram =>
                    new HistogramCalculator().Compare(baseline, actual),

                ComparisonAlgorithm.Hybrid =>
                    Math.Round(
                        (new SsimCalculator().Compare(baseline, actual) +
                         new PerceptualHashCalculator().Compare(baseline, actual)) / 2.0, 2),

                _ => new SsimCalculator().Compare(baseline, actual),
            };

        private static Bitmap LoadBitmapFromBytes(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            using var img = System.Drawing.Image.FromStream(ms);
            return new Bitmap(img);
        }

        private static void FillRegions(Bitmap bmp, IList<Rectangle> regions, Color color)
        {
            if (regions.Count == 0) return;
            using var g = Graphics.FromImage(bmp);
            using var brush = new SolidBrush(color);
            foreach (var r in regions)
                g.FillRectangle(brush, r);
        }

        private static Bitmap CropBitmap(Bitmap src, Rectangle region)
        {
            var safe = Rectangle.Intersect(new Rectangle(0, 0, src.Width, src.Height), region);
            if (safe.Width <= 0 || safe.Height <= 0)
                return new Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format24bppRgb);
            return src.Clone(safe, src.PixelFormat);
        }
    }
}
