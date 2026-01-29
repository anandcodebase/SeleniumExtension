using System.Collections.Generic;
using System.Drawing;

namespace SimpleSeleniumSupport
{
    public sealed class ImageComparisonOptions
    {
        // ================= BACKWARD COMPAT =================
        public ImageComparator.ComparisonMode Mode { get; set; }
            = ImageComparator.ComparisonMode.WholeImage;

        public Rectangle? Region { get; set; }

        // ================= PROVIDER =================
        public string Provider { get; set; } = "ollama";
        public int TimeoutSeconds { get; set; } = 300;

        public string OllamaBaseUrl { get; set; }
        public string OllamaModel { get; set; }
        public string OllamaApiKey { get; set; }

        // 🔥 MISSING AUTH PROPERTIES (FIX)
        public string OllamaAuthHeader { get; set; } = "Authorization";
        public string OllamaAuthScheme { get; set; } = "Bearer";

        // ================= TEXT / OCR =================
        public bool EnableTextExtraction { get; set; }

        // ================= VISUAL DIFF =================
        public List<Rectangle> CompareRegions { get; } = new();
        public List<Rectangle> IgnoreRegions { get; } = new();

        public string VisualDiffOutputDirectory { get; set; }
    }
}
