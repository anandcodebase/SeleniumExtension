using System;
using System.Drawing;
using System.IO;

namespace SimpleSeleniumSupport.Image
{
    public static class VisualBaselineManager
    {
        private const string EnvAutoBaseline = "AUTO_BASELINE";

        /// <summary>
        /// Resolves the baseline path for a test, always using a per-browser/viewport directory.
        /// The <c>AUTO_BASELINE</c> environment variable controls automatic creation.
        /// </summary>
        public static VisualBaselineContext Resolve(
            string testId,
            string browser,
            Size viewport,
            string artifactsRoot,
            string actualImagePath)
            => ResolveCore(testId, browser, viewport, artifactsRoot, actualImagePath,
                           browserTagged: true, autoBaseline: false);

        /// <summary>
        /// Resolves the baseline path, with an explicit <paramref name="autoBaseline"/> override.
        /// </summary>
        public static VisualBaselineContext Resolve(
            string testId,
            string browser,
            Size viewport,
            string artifactsRoot,
            string actualImagePath,
            bool autoBaseline)
            => ResolveCore(testId, browser, viewport, artifactsRoot, actualImagePath,
                           browserTagged: true, autoBaseline: autoBaseline);

        /// <summary>
        /// Resolves the baseline path with full control over browser-tagging.
        /// <para>
        /// When <paramref name="browserTagged"/> is <see langword="true"/> (default), the
        /// baseline is stored under <c>VisualBaselines/{testId}/{browser}_{W}x{H}/baseline.png</c>
        /// so each browser/viewport combination keeps its own baseline.
        /// When <see langword="false"/>, a single shared baseline is used at
        /// <c>VisualBaselines/{testId}/baseline.png</c> regardless of browser or viewport.
        /// </para>
        /// </summary>
        public static VisualBaselineContext Resolve(
            string testId,
            string browser,
            Size viewport,
            string artifactsRoot,
            string actualImagePath,
            bool browserTagged,
            bool autoBaseline)
            => ResolveCore(testId, browser, viewport, artifactsRoot, actualImagePath,
                           browserTagged, autoBaseline);

        // ── Private core ──────────────────────────────────────────────────────

        private static VisualBaselineContext ResolveCore(
            string testId,
            string browser,
            Size viewport,
            string artifactsRoot,
            string actualImagePath,
            bool browserTagged,
            bool autoBaseline)
        {
            // Also respect the AUTO_BASELINE env var (CI-friendly override)
            var envVal = Environment.GetEnvironmentVariable(EnvAutoBaseline);
            bool effectiveAuto = autoBaseline || envVal == "1" || envVal == "true";

            string dir = browserTagged
                ? Path.Combine(artifactsRoot, "VisualBaselines", Sanitize(testId),
                               $"{browser}_{viewport.Width}x{viewport.Height}")
                : Path.Combine(artifactsRoot, "VisualBaselines", Sanitize(testId));

            Directory.CreateDirectory(dir);

            var baselinePath = Path.Combine(dir, "baseline.png");

            bool exists  = File.Exists(baselinePath);
            bool created = false;
            bool updated = false;

            if (!exists)
            {
                if (!effectiveAuto)
                    throw new InvalidOperationException(
                        $"Baseline not found for '{testId}'. " +
                        $"Set the environment variable AUTO_BASELINE=1 to create it automatically.");

                File.Copy(actualImagePath, baselinePath);
                created = true;
            }
            else if (effectiveAuto)
            {
                File.Copy(actualImagePath, baselinePath, overwrite: true);
                updated = true;
            }

            return new VisualBaselineContext
            {
                TestId          = testId,
                Browser         = browser,
                Viewport        = viewport,
                BaselinePath    = baselinePath,
                ActualPath      = actualImagePath,
                BaselineCreated = created,
                BaselineUpdated = updated
            };
        }

        private static string Sanitize(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s;
        }
    }
}
