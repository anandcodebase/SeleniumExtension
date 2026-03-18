using OpenQA.Selenium;
using System;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;

namespace SimpleSeleniumSupport.OCR
{
    /// <summary>
    /// Extension methods that apply OCR to WebDriver screenshots and element regions.
    /// </summary>
    public static class OcrExtensions
    {
        // ── IWebDriver ────────────────────────────────────────────────────────

        /// <summary>
        /// Takes a full-page screenshot and extracts all visible text via OCR.
        /// </summary>
        public static string GetScreenshotText(this IWebDriver driver, OcrOptions? opts = null)
        {
            var screenshot = ((ITakesScreenshot)driver).GetScreenshot();
            using var bmp = LoadBitmap(screenshot.AsByteArray);
            return OcrEngine.ExtractText(bmp, opts);
        }

        /// <summary>
        /// Takes a full-page screenshot, crops it to <paramref name="region"/>
        /// (screen-pixel coordinates), and extracts text from that region only.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown when <paramref name="region"/> does not intersect the screenshot bounds.
        /// </exception>
        public static string GetRegionText(
            this IWebDriver driver, Rectangle region, OcrOptions? opts = null)
        {
            var screenshot = ((ITakesScreenshot)driver).GetScreenshot();
            using var full = LoadBitmap(screenshot.AsByteArray);
            var safe = Rectangle.Intersect(
                new Rectangle(0, 0, full.Width, full.Height), region);
            if (safe.IsEmpty)
                throw new ArgumentOutOfRangeException(nameof(region),
                    "Region does not intersect the screenshot bounds.");
            using var crop = full.Clone(safe, full.PixelFormat);
            return OcrEngine.ExtractText(crop, opts);
        }

        // ── IWebElement ───────────────────────────────────────────────────────

        /// <summary>
        /// Takes a full-page screenshot, crops it to the element's bounding box,
        /// and extracts text from that region via OCR.
        /// Useful for elements that render text as images (canvas, SVG, custom fonts).
        /// </summary>
        public static string GetElementText(
            this IWebElement element, IWebDriver driver, OcrOptions? opts = null)
        {
            var loc    = element.Location;
            var size   = element.Size;
            var region = new Rectangle(loc.X, loc.Y, size.Width, size.Height);
            return driver.GetRegionText(region, opts);
        }

        /// <summary>
        /// Asserts that OCR text extracted from the element contains <paramref name="expected"/>.
        /// </summary>
        /// <param name="element">Source element to crop and OCR.</param>
        /// <param name="driver">Active WebDriver used to take the screenshot.</param>
        /// <param name="expected">Text the OCR result must contain.</param>
        /// <param name="comparison">String comparison; default case-insensitive.</param>
        /// <param name="opts">OCR options; null = defaults.</param>
        /// <exception cref="InvalidOperationException">Thrown when text is not found.</exception>
        public static void AssertTextContains(
            this IWebElement element,
            IWebDriver driver,
            string expected,
            StringComparison comparison = StringComparison.OrdinalIgnoreCase,
            OcrOptions? opts = null)
        {
            var actual = element.GetElementText(driver, opts);
            if (!actual.Contains(expected, comparison))
                throw new InvalidOperationException(
                    $"OCR assertion failed.\n" +
                    $"Expected to contain : '{expected}'\n" +
                    $"Actual OCR text     : '{actual}'");
        }

        /// <summary>
        /// Asserts that OCR text extracted from the element matches <paramref name="pattern"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown when pattern does not match.</exception>
        public static void AssertTextMatches(
            this IWebElement element,
            IWebDriver driver,
            Regex pattern,
            OcrOptions? opts = null)
        {
            var actual = element.GetElementText(driver, opts);
            if (!pattern.IsMatch(actual))
                throw new InvalidOperationException(
                    $"OCR assertion failed.\n" +
                    $"Pattern            : '{pattern}'\n" +
                    $"Actual OCR text    : '{actual}'");
        }

        // ── Helper ────────────────────────────────────────────────────────────

        private static Bitmap LoadBitmap(byte[] bytes)
        {
            using var ms = new MemoryStream(bytes);
            using var img = System.Drawing.Image.FromStream(ms);
            return new Bitmap(img);  // deep copy — stream can close safely
        }
    }
}
