namespace SimpleSeleniumSupport.Reporting
{
    public sealed class VisualGridRow
    {
        public string? TestId { get; set; }
        public string? Browser { get; set; }
        public string? Viewport { get; set; }

        public double Similarity { get; set; }
        public string? BaselineStatus { get; set; }

        public string? BaselineImage { get; set; }
        public string? ActualImage { get; set; }
        public string? HeatmapImage { get; set; }

        public string? Reasoning { get; set; }

        // ================= NEW METRICS =================
        public double? SsimPercent { get; set; }
        public double? EdgePercent { get; set; }
        public double? PixelDiffPercent { get; set; }
        public bool Passed { get; set; }
        public double Threshold { get; set; }
        public string? AiReasoning { get; set; }
        public string? DiffImage { get; set; }
    }
}
