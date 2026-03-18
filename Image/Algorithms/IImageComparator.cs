using System.Drawing;

namespace SimpleSeleniumSupport.Image.Algorithms
{
    /// <summary>
    /// Common interface implemented by all image comparison algorithms.
    /// </summary>
    internal interface IImageComparator
    {
        /// <summary>Short name shown in reports (e.g. "SSIM", "pHash", "PixelDiff").</summary>
        string AlgorithmName { get; }

        /// <summary>
        /// Compare two same-sized bitmaps and return a similarity score in [0, 100].
        /// 100 = identical, 0 = completely different.
        /// </summary>
        double Compare(Bitmap baseline, Bitmap actual);
    }
}
