using System.Drawing;
using System.Drawing.Imaging;

namespace SimpleSeleniumSupport.Diagnostics
{
    /// <summary>
    /// Options for annotating a screenshot with failure information.
    /// </summary>
    public sealed class AnnotationOptions
    {
        /// <summary>Text shown in the red banner at the top of the image (e.g. exception message).</summary>
        public string? TopBanner { get; set; }

        /// <summary>Text shown in the grey info bar at the bottom (e.g. test name + timestamp).</summary>
        public string? BottomBanner { get; set; }

        public Color BannerColor    { get; set; } = Color.FromArgb(200, 220, 30, 30);
        public Color TextColor      { get; set; } = Color.White;
        public float FontSize       { get; set; } = 13f;
        public bool  AddTimestamp   { get; set; } = true;

        /// <summary>Optional rectangle to highlight (e.g. the failing element's bounding box).</summary>
        public Rectangle? HighlightRegion { get; set; }
        public Color HighlightColor { get; set; } = Color.Red;
        public int   HighlightWidth { get; set; } = 3;
    }

    /// <summary>
    /// Draws failure information overlays on a PNG screenshot using
    /// <c>System.Drawing.Common</c> (already a library dependency).
    /// </summary>
    public static class ScreenshotAnnotator
    {
        /// <summary>
        /// Annotates a screenshot with a failure message banner and optional info bar.
        /// </summary>
        /// <param name="pngBytes">Raw PNG bytes from <c>ITakesScreenshot.GetScreenshot().AsByteArray</c>.</param>
        /// <param name="options">Annotation options.</param>
        /// <returns>Annotated PNG bytes.</returns>
        public static byte[] Annotate(byte[] pngBytes, AnnotationOptions options)
        {
            using var ms  = new MemoryStream(pngBytes);
            using var bmp = new Bitmap(ms);
            using var g   = Graphics.FromImage(bmp);

            g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.SingleBitPerPixelGridFit;

            using var font = new Font("Segoe UI", options.FontSize, FontStyle.Regular, GraphicsUnit.Pixel);
            var bannerH    = (int)(options.FontSize * 2.2f);

            // ── Top banner ────────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(options.TopBanner))
            {
                using var brush = new SolidBrush(options.BannerColor);
                g.FillRectangle(brush, 0, 0, bmp.Width, bannerH);
                using var textBrush = new SolidBrush(options.TextColor);
                var text = options.TopBanner!;
                // Truncate if too long to fit
                while (text.Length > 4 && g.MeasureString(text, font).Width > bmp.Width - 16)
                    text = text[..^4] + "…";
                g.DrawString(text, font, textBrush, new PointF(8, (bannerH - options.FontSize) / 2f));
            }

            // ── Bottom info bar ───────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(options.BottomBanner) || options.AddTimestamp)
            {
                var infoText = options.BottomBanner ?? "";
                if (options.AddTimestamp)
                    infoText = $"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss UTC}  {infoText}".TrimEnd();

                using var infoBrush = new SolidBrush(Color.FromArgb(200, 30, 30, 30));
                g.FillRectangle(infoBrush, 0, bmp.Height - bannerH, bmp.Width, bannerH);
                using var textBrush = new SolidBrush(options.TextColor);
                g.DrawString(infoText, font, textBrush, new PointF(8, bmp.Height - bannerH + (bannerH - options.FontSize) / 2f));
            }

            // ── Element highlight box ─────────────────────────────────────────
            if (options.HighlightRegion.HasValue)
            {
                using var pen = new Pen(options.HighlightColor, options.HighlightWidth);
                g.DrawRectangle(pen, options.HighlightRegion.Value);
            }

            using var outMs = new MemoryStream();
            bmp.Save(outMs, ImageFormat.Png);
            return outMs.ToArray();
        }

        /// <summary>
        /// Convenience overload that draws a standard failure overlay:
        /// red top banner with the failure message, grey bottom bar with test name + UTC timestamp.
        /// </summary>
        /// <param name="pngBytes">Raw PNG bytes.</param>
        /// <param name="failureMessage">The exception or assertion message.</param>
        /// <param name="testName">Optional test name shown in the bottom bar.</param>
        /// <param name="timestamp">Optional timestamp; defaults to <see cref="DateTime.UtcNow"/>.</param>
        public static byte[] AnnotateFailure(
            byte[] pngBytes,
            string failureMessage,
            string? testName = null,
            DateTime? timestamp = null)
        {
            var ts     = timestamp ?? DateTime.UtcNow;
            var bottom = $"{ts:yyyy-MM-dd HH:mm:ss UTC}" + (testName != null ? $"  |  {testName}" : "");
            return Annotate(pngBytes, new AnnotationOptions
            {
                TopBanner    = failureMessage,
                BottomBanner = bottom,
                AddTimestamp = false   // already embedded in BottomBanner
            });
        }
    }
}
