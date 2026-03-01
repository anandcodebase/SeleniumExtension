using System.Drawing;

namespace SimpleSeleniumSupport.Image
{
    public sealed class ImageComparisonResult
    {
        public double SimilarityPercent { get; set; }
        public double? VisualSimilarityPercent { get; set; }

        // ================= COMPONENT SCORES =================
        public double? SsimPercent { get; set; }
        public double? EdgePercent { get; set; }

        // ================= PIXEL DIFF =================
        public double? PixelDiffPercent { get; set; }
        public int? PixelDiffCount { get; set; }
        public int? TotalPixels { get; set; }

        // ================= PASS / FAIL =================
        public bool Passed { get; set; }
        public double Threshold { get; set; }

        // ================= BACKWARD COMPAT =================
        public double? TextSimilarityPercent { get; set; }
        public Rectangle? RegionCompared { get; set; }

        // ================= REASONING =================
        public string? Reasoning { get; set; }
        public string? AiReasoning { get; set; }

        // ================= ARTIFACTS =================
        public string? HeatmapPath { get; set; }
        public string? DiffImagePath { get; set; }
    }
}
