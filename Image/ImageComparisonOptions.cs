using System.Collections.Generic;
using System.Drawing;

namespace SimpleSeleniumSupport.Image
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
        public string OllamaAuthHeader { get; set; } = "Authorization";
        public string OllamaAuthScheme { get; set; } = "Bearer";

        // ================= TEXT / OCR =================
        public bool EnableTextExtraction { get; set; }

        // ================= VISUAL DIFF =================
        public List<Rectangle> CompareRegions { get; } = new();
        public List<Rectangle> IgnoreRegions { get; } = new();

        public string VisualDiffOutputDirectory { get; set; }

        // ================= SCORING WEIGHTS =================

        /// <summary>Weight of SSIM in final score (0..1). Default 0.7.</summary>
        public double SsimWeight { get; set; } = 0.7;

        /// <summary>Weight of edge similarity in final score (0..1). Default 0.3.</summary>
        public double EdgeWeight { get; set; } = 0.3;

        /// <summary>
        /// Downscale longest edge to this size for perceptual comparison.
        /// 0 = use original resolution. Default 0.
        /// </summary>
        public int NormalizeSize { get; set; } = 0;

        // ================= THRESHOLD =================

        /// <summary>
        /// Pass/fail threshold (0-100). Comparison passes if SimilarityPercent >= Threshold.
        /// Default 95.0.
        /// </summary>
        public double Threshold { get; set; } = 95.0;

        // ================= TOLERANCE =================

        /// <summary>
        /// Pixel radius for anti-aliasing neighbor check. 0 = disabled. Default 2.
        /// Before counting a pixel as changed, check if any neighbor within this radius
        /// in the other image has a similar color.
        /// </summary>
        public int AntiAliasingTolerance { get; set; } = 2;

        /// <summary>
        /// Per-channel color distance below which two pixels are considered identical (0-255).
        /// Default 0 (exact match).
        /// </summary>
        public int PixelTolerance { get; set; } = 0;

        // ================= AI =================

        /// <summary>
        /// When true, also runs AI-based comparison via the configured provider
        /// and merges the AI reasoning into the result. Default false.
        /// </summary>
        public bool EnableAI { get; set; }
    }
}
