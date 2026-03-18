using System.Collections.Generic;
using System.Linq;
using SimpleSeleniumSupport.Reporting;

namespace SimpleSeleniumSupport.AI.Sanitization
{
    /// <summary>
    /// Sanitizes <see cref="TestResult"/> objects before they are serialized into a report,
    /// preventing sensitive data (credentials, tokens, codebase paths) from appearing in
    /// exported HTML / Excel / JSON artefacts.
    /// <para>
    /// Always returns a <b>new cloned instance</b> — the original <see cref="TestResult"/>
    /// is never mutated.
    /// </para>
    /// </summary>
    public static class ReportSanitizer
    {
        /// <summary>
        /// Returns a sanitized shallow clone of <paramref name="result"/>.
        /// Non-sensitive identity, timing, and environment fields are copied as-is.
        /// Text fields (exception message, stack trace, AI analysis) and path fields
        /// are run through <see cref="PromptSanitizer.Sanitize"/>.
        /// </summary>
        public static TestResult SanitizeResult(
            TestResult result, SanitizationOptions? opts = null)
        {
            opts ??= new SanitizationOptions();

            var clone = new TestResult
            {
                // ── Identity (non-sensitive) ──────────────────────────────────
                TestName       = result.TestName,
                TestSuite      = result.TestSuite,
                FullName       = result.FullName,
                Category       = result.Category,
                Tags           = result.Tags,
                RunName        = result.RunName,

                // ── Outcome ───────────────────────────────────────────────────
                Status         = result.Status,
                DurationMs     = result.DurationMs,
                StartTime      = result.StartTime,

                // ── Environment (non-sensitive) ───────────────────────────────
                Browser        = result.Browser,
                Environment    = result.Environment,
                MachineName    = result.MachineName,

                // ── Analytics ─────────────────────────────────────────────────
                RetryCount     = result.RetryCount,
                FlakinessScore = result.FlakinessScore,
                SkipReason     = result.SkipReason,

                // ── Exception type (class name only — never contains values) ──
                ExceptionType  = result.ExceptionType,

                // ── Scrubbed text fields ──────────────────────────────────────
                ExceptionMessage = Scrub(result.ExceptionMessage, opts),
                StackTrace       = Scrub(result.StackTrace, opts),
                AssertMessage    = Scrub(result.AssertMessage, opts),
                AiAnalysis       = Scrub(result.AiAnalysis, opts),

                // ── Path fields → path token replacement ─────────────────────
                ScreenshotPath    = ScrubPath(result.ScreenshotPath, opts),
                ScreencastPath    = result.ScreencastPath,    // remote URLs kept intact
                DiagnosticsFolder = ScrubPath(result.DiagnosticsFolder, opts),
                NetworkHarPath    = ScrubPath(result.NetworkHarPath, opts),
                NetworkExcelPath  = ScrubPath(result.NetworkExcelPath, opts),
            };

            // Scrub custom property values (keys are never sensitive)
            foreach (var kv in result.CustomProperties)
                clone.CustomProperties[kv.Key] = Scrub(kv.Value, opts) ?? "";

            return clone;
        }

        /// <summary>
        /// Returns a sanitized clone of every result in the collection.
        /// </summary>
        public static IEnumerable<TestResult> SanitizeResults(
            IEnumerable<TestResult> results, SanitizationOptions? opts = null)
            => results.Select(r => SanitizeResult(r, opts));

        // ── Private helpers ───────────────────────────────────────────────────

        private static string? Scrub(string? value, SanitizationOptions opts)
            => value == null ? null : PromptSanitizer.Sanitize(value, opts);

        private static string? ScrubPath(string? path, SanitizationOptions opts)
        {
            if (!opts.RedactFilePaths || string.IsNullOrEmpty(path)) return path;
            return PromptSanitizer.Sanitize(path, opts);
        }
    }
}
