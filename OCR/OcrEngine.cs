using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using Tesseract;

namespace SimpleSeleniumSupport.OCR
{
    /// <summary>
    /// Extracts text from images using the Tesseract OCR engine (v5.x NuGet package).
    /// <para>
    /// <b>Prerequisites:</b> Add <c>Tesseract</c> NuGet package to your project and
    /// download Tesseract tessdata language files from
    /// https://github.com/tesseract-ocr/tessdata — then set
    /// <see cref="SimpleSeleniumSupportDefaults.OcrTessdataPath"/> to the folder
    /// containing the <c>.traineddata</c> files.
    /// </para>
    /// </summary>
    public static class OcrEngine
    {
        // TesseractEngine init loads tessdata from disk — cache one per (path, lang) pair.
        // WhitelistChars callers bypass the cache (SetVariable is not thread-safe to call
        // on a shared engine), so they get a fresh short-lived engine.
        // Each cached entry uses a SemaphoreSlim(1) because TesseractEngine.Process is
        // not documented as thread-safe; serialising access avoids races.
        private sealed record EngineEntry(Lazy<TesseractEngine> Lazy, SemaphoreSlim Gate);

        private static readonly ConcurrentDictionary<(string Path, string Lang), EngineEntry>
            _engineCache = new();
        /// <summary>
        /// Extracts all readable text from the supplied <paramref name="image"/>.
        /// </summary>
        /// <param name="image">Source bitmap (screenshot or cropped region).</param>
        /// <param name="opts">OCR options; null = library defaults.</param>
        /// <returns>Extracted text string (may be empty if no text is found).</returns>
        public static string ExtractText(Bitmap image, OcrOptions? opts = null)
        {
            opts ??= new OcrOptions();
            using var ms = new MemoryStream();
            image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            return ExtractFromBytes(ms.ToArray(), opts);
        }

        /// <summary>
        /// Extracts all readable text from a raw PNG/JPG byte array.
        /// </summary>
        /// <param name="imageBytes">Raw image bytes.</param>
        /// <param name="opts">OCR options; null = library defaults.</param>
        public static string ExtractText(byte[] imageBytes, OcrOptions? opts = null)
            => ExtractFromBytes(imageBytes, opts ?? new OcrOptions());

        // ── Internal ──────────────────────────────────────────────────────────

        private static string ExtractFromBytes(byte[] pngBytes, OcrOptions opts)
        {
            var lang         = opts.Language     ?? SimpleSeleniumSupportDefaults.OcrLanguage;
            var tessdataPath = opts.TessdataPath ?? SimpleSeleniumSupportDefaults.OcrTessdataPath;

            if (!Directory.Exists(tessdataPath))
                throw new InvalidOperationException(
                    $"Tesseract tessdata directory not found: '{tessdataPath}'. " +
                    $"Download language files (e.g. eng.traineddata) from " +
                    $"https://github.com/tesseract-ocr/tessdata and set " +
                    $"SimpleSeleniumSupportDefaults.OcrTessdataPath.");

            // When a whitelist is set we use a short-lived engine (SetVariable mutates state).
            // Otherwise, reuse a cached engine to avoid repeated tessdata I/O.
            bool useCache = string.IsNullOrEmpty(opts.WhitelistChars);

            if (!useCache)
            {
                using var fresh = new TesseractEngine(tessdataPath, lang, EngineMode.Default);
                fresh.SetVariable("tessedit_char_whitelist", opts.WhitelistChars!);
                return RunOcr(fresh, pngBytes, opts);
            }

            var key   = (tessdataPath, lang);
            var entry = _engineCache.GetOrAdd(key, k => new EngineEntry(
                new Lazy<TesseractEngine>(
                    () => new TesseractEngine(k.Path, k.Lang, EngineMode.Default),
                    LazyThreadSafetyMode.ExecutionAndPublication),
                new SemaphoreSlim(1, 1)));

            entry.Gate.Wait();
            try
            {
                return RunOcr(entry.Lazy.Value, pngBytes, opts);
            }
            finally
            {
                entry.Gate.Release();
            }
        }

        private static string RunOcr(TesseractEngine engine, byte[] pngBytes, OcrOptions opts)
        {
            using var pix  = Pix.LoadFromMemory(pngBytes);
            using var page = engine.Process(pix, (PageSegMode)opts.PageSegMode);
            var raw = page.GetText() ?? "";
            if (opts.NormalizeWhitespace)
                raw = Regex.Replace(raw.Trim(), @"\s+", " ");
            return raw;
        }
    }
}
