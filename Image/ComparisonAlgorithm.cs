namespace SimpleSeleniumSupport.Image
{
    /// <summary>
    /// Selects the algorithm used by <see cref="VisualDiffEngine"/> when comparing two images.
    /// </summary>
    public enum ComparisonAlgorithm
    {
        /// <summary>
        /// Luminance-based Structural Similarity Index combined with Sobel edge similarity.
        /// Best general-purpose choice. (Default)
        /// </summary>
        Ssim,

        /// <summary>
        /// DCT-based perceptual hash (pHash).
        /// Robust to minor crops, compression artefacts, and brightness shifts.
        /// </summary>
        PerceptualHash,

        /// <summary>
        /// Exact per-pixel comparison.
        /// Fast; sensitive to sub-pixel rendering differences and anti-aliasing.
        /// </summary>
        PixelDiff,

        /// <summary>
        /// Greyscale histogram Pearson correlation.
        /// Tolerant of layout shifts; useful for colour palette checks.
        /// </summary>
        Histogram,

        /// <summary>
        /// Weighted average of SSIM and pHash scores (50/50).
        /// Balances structural accuracy with perceptual robustness.
        /// </summary>
        Hybrid,
    }
}
