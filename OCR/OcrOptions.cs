namespace SimpleSeleniumSupport.OCR
{
    /// <summary>
    /// Configuration for OCR text extraction via Tesseract.
    /// </summary>
    public sealed class OcrOptions
    {
        /// <summary>
        /// Tesseract language pack (e.g. "eng", "fra", "deu").
        /// Defaults to <see cref="SimpleSeleniumSupportDefaults.OcrLanguage"/>.
        /// </summary>
        public string? Language { get; set; }

        /// <summary>
        /// Tesseract page segmentation mode.
        /// <list type="bullet">
        ///   <item>3 = Auto (default — fully automatic page segmentation)</item>
        ///   <item>6 = Single uniform block of text</item>
        ///   <item>7 = Single text line</item>
        ///   <item>8 = Single word</item>
        ///   <item>10 = Single character</item>
        ///   <item>11 = Sparse text</item>
        /// </list>
        /// </summary>
        public int PageSegMode { get; set; } = 3;

        /// <summary>
        /// Optional whitelist of characters Tesseract is allowed to recognise.
        /// Example: <c>"0123456789"</c> restricts output to digits.
        /// Null = no whitelist (default).
        /// </summary>
        public string? WhitelistChars { get; set; }

        /// <summary>
        /// Path to the Tesseract tessdata directory.
        /// Defaults to <see cref="SimpleSeleniumSupportDefaults.OcrTessdataPath"/>.
        /// </summary>
        public string? TessdataPath { get; set; }

        /// <summary>
        /// When true, trims leading/trailing whitespace and collapses internal
        /// whitespace runs in the returned text. Default true.
        /// </summary>
        public bool NormalizeWhitespace { get; set; } = true;
    }
}
