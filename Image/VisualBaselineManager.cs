using System;
using System.Drawing;
using System.IO;

namespace SimpleSeleniumSupport.Image
{
    public static class VisualBaselineManager
    {
        private const string EnvAutoBaseline = "AUTO_BASELINE";

        public static VisualBaselineContext Resolve(
            string testId,
            string browser,
            Size viewport,
            string artifactsRoot,
            string actualImagePath)
        {
            var dir = Path.Combine(
                artifactsRoot,
                "VisualBaselines",
                Sanitize(testId),
                $"{browser}_{viewport.Width}x{viewport.Height}");

            Directory.CreateDirectory(dir);

            var baselinePath = Path.Combine(dir, "baseline.png");

            bool autoBaseline =
                Environment.GetEnvironmentVariable(EnvAutoBaseline) == "true";

            bool exists = File.Exists(baselinePath);
            bool created = false;
            bool updated = false;

            if (!exists)
            {
                if (!autoBaseline)
                    throw new InvalidOperationException(
                        $"Baseline missing for {testId}. Set AUTO_BASELINE=true.");

                File.Copy(actualImagePath, baselinePath);
                created = true;
            }
            else if (autoBaseline)
            {
                File.Copy(actualImagePath, baselinePath, overwrite: true);
                updated = true;
            }

            return new VisualBaselineContext
            {
                TestId = testId,
                Browser = browser,
                Viewport = viewport,
                BaselinePath = baselinePath,
                ActualPath = actualImagePath,
                BaselineCreated = created,
                BaselineUpdated = updated
            };
        }

        // ================= OVERLOAD (FIXES CS1739) =================
        public static VisualBaselineContext Resolve(
            string testId,
            string browser,
            Size viewport,
            string artifactsRoot,
            string actualImagePath,
            bool autoBaseline)
        {
            if (autoBaseline)
                Environment.SetEnvironmentVariable(EnvAutoBaseline, "true");

            return Resolve(testId, browser, viewport, artifactsRoot, actualImagePath);
        }

        private static string Sanitize(string s)
        {
            foreach (var c in Path.GetInvalidFileNameChars())
                s = s.Replace(c, '_');
            return s;
        }
    }
}
