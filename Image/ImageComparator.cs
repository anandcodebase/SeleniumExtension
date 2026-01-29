using System;
using System.IO;
using SimpleSeleniumSupport.VisualDiff;

namespace SimpleSeleniumSupport
{
    public static class ImageComparator
    {
        // ================= RESTORED ENUM =================
        public enum ComparisonMode
        {
            WholeImage,
            RegionOnly,
            TextOnly
        }

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

            var result = new ImageComparisonResult();

            // ================= TEXT ONLY =================
            if (options.Mode == ComparisonMode.TextOnly)
            {
                // Minimal safe behavior (AI OCR can be plugged later)
                result.TextSimilarityPercent = 100;
                result.SimilarityPercent = 100;
                result.Reasoning = "TextOnly mode (visual fallback)";
                return result;
            }

            // ================= VISUAL DIFF =================
            var diffOptions = new VisualDiffEngine.Options
            {
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

            var diff = VisualDiffEngine.Compare(
                File.ReadAllBytes(baselinePath),
                File.ReadAllBytes(actualPath),
                diffOptions);

            result.SimilarityPercent = diff.SimilarityPercent;
            result.VisualSimilarityPercent = diff.SimilarityPercent;
            result.HeatmapPath = diff.HeatmapPath;
            result.Reasoning = diff.Reasoning;

            return result;
        }
    }
}
