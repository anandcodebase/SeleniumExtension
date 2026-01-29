using System.Drawing;

namespace SimpleSeleniumSupport
{
    public sealed class ImageComparisonResult
    {
        public double SimilarityPercent { get; set; }
        public double? VisualSimilarityPercent { get; set; }

        // ================= BACKWARD COMPAT =================
        public double? TextSimilarityPercent { get; set; }
        public Rectangle? RegionCompared { get; set; }

        public string Reasoning { get; set; }
        public string HeatmapPath { get; set; }
    }
}
