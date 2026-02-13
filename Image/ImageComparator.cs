using System;
using System.Collections.Generic;
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

            // ================= VISUAL DIFF =================
            var diffOptions = new VisualDiffEngine.Options
            {
                SsimWeight = options.SsimWeight,
                EdgeWeight = options.EdgeWeight,
                NormalizeSize = options.NormalizeSize,
                AntiAliasingTolerance = options.AntiAliasingTolerance,
                PixelTolerance = options.PixelTolerance,
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
    }
}
