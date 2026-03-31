using Microsoft.Extensions.Logging;
using SimpleSeleniumSupport.Analytics;
using SimpleSeleniumSupport.CiExport;
using SimpleSeleniumSupport.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SimpleSeleniumSupport.Reporting
{
    /// <summary>
    /// Exports a collection of <see cref="TestResult"/> objects to a production-ready
    /// interactive HTML report (GridJS + Bootstrap 5).
    /// <list type="bullet">
    ///   <item><description>
    ///     <see cref="Export"/> — folder output: <c>index.html</c> + <c>data.json</c> + <c>manifest.json</c>.
    ///     Scales to tens of thousands of tests; data loaded client-side.
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="ExportSingleFile"/> — one self-contained <c>.html</c> file with all data embedded inline.
    ///     No external dependencies — open directly or attach to CI artefacts.
    ///   </description></item>
    ///   <item><description>
    ///     <see cref="ExportToExcel"/> — <c>.xlsx</c> workbook with one row per test result.
    ///   </description></item>
    /// </list>
    /// </summary>
    public static class TestRunReportExporter
    {
        private static readonly ILogger _log =
            LibraryLogger.ForCategory("SimpleSeleniumSupport.Reporting.TestRunReportExporter");

        // ── Public API ─────────────────────────────────────────────────────────

        /// <summary>
        /// Exports <paramref name="results"/> to a report folder containing
        /// <c>index.html</c>, <c>data.json</c>, and <c>manifest.json</c>.
        /// The HTML loads data client-side, so it scales well for large result sets.
        /// Open <c>index.html</c> in a browser (must be served or opened from disk with local-file access).
        /// </summary>
        /// <param name="results">Test results to include.</param>
        /// <param name="outFolderRoot">Parent directory under which the timestamped report folder is created.</param>
        /// <param name="reportName">Display name shown in the report title and used to name the folder.</param>
        /// <returns>Absolute path of the generated report folder.</returns>
        public static string Export(
            IEnumerable<TestResult> results,
            string outFolderRoot,
            string? reportName = null,
            AI.Sanitization.SanitizationOptions? sanitize = null)
        {
            if (string.IsNullOrWhiteSpace(outFolderRoot))
                throw new ArgumentNullException(nameof(outFolderRoot));
            ValidateWebUrlMode();

            Directory.CreateDirectory(outFolderRoot);

            var safeName     = string.IsNullOrWhiteSpace(reportName) ? "test-report" : MakeSafeFileName(reportName!);
            var timestamp    = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var reportFolder = GetUniqueFolderPath(outFolderRoot, $"{safeName}-{timestamp}");
            Directory.CreateDirectory(reportFolder);

            var list = (results ?? Enumerable.Empty<TestResult>()).ToList();
            if (sanitize != null)
                list = AI.Sanitization.ReportSanitizer.SanitizeResults(list, sanitize).ToList();
            var jsonOptions = CreateJsonOptions();
            var rows        = list.Select((r, idx) => BuildRow(r, idx)).ToList();
            var (runGroups, runsJson) = ComputeRunData(list, jsonOptions);

            // ── data.json ─────────────────────────────────────────────────────
            File.WriteAllText(
                Path.Combine(reportFolder, "data.json"),
                JsonSerializer.Serialize(rows, jsonOptions),
                Encoding.UTF8);

            // ── manifest.json ─────────────────────────────────────────────────
            File.WriteAllText(
                Path.Combine(reportFolder, "manifest.json"),
                JsonSerializer.Serialize(BuildManifest(list, reportName, safeName, runGroups), jsonOptions),
                Encoding.UTF8);

            // ── index.html (loads data.json via fetch) ────────────────────────
            File.WriteAllText(
                Path.Combine(reportFolder, "index.html"),
                BuildHtml(FetchInitScript, runsJson, reportName, safeName),
                Encoding.UTF8);

            // ── Analytics history (opt-in) ────────────────────────────────────
            if (SimpleSeleniumSupportDefaults.AnalyticsEnabled)
                HistoryStore.Append(list, reportName ?? "Test Run");

            return Path.GetFullPath(reportFolder);
        }

        /// <summary>
        /// Exports <paramref name="results"/> to a <b>single self-contained <c>.html</c> file</b>
        /// with <c>data.json</c> and <c>manifest.json</c> content embedded inline as JavaScript.
        /// <para>
        /// The file has no external data dependencies and can be opened directly from disk,
        /// e-mailed, or attached to CI artefacts without any additional files.
        /// For very large result sets (&gt;5 000 rows) the file size may exceed several MB;
        /// prefer <see cref="Export"/> in those cases.
        /// </para>
        /// </summary>
        /// <param name="results">Test results to include.</param>
        /// <param name="outFolderRoot">Directory where the <c>.html</c> file is written.</param>
        /// <param name="reportName">Display name shown in the report title and used to name the file.</param>
        /// <returns>Absolute path of the generated <c>.html</c> file.</returns>
        public static string ExportSingleFile(
            IEnumerable<TestResult> results,
            string outFolderRoot,
            string? reportName = null,
            AI.Sanitization.SanitizationOptions? sanitize = null)
        {
            if (string.IsNullOrWhiteSpace(outFolderRoot))
                throw new ArgumentNullException(nameof(outFolderRoot));
            ValidateWebUrlMode();

            Directory.CreateDirectory(outFolderRoot);

            var safeName  = string.IsNullOrWhiteSpace(reportName) ? "test-report" : MakeSafeFileName(reportName!);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var outPath   = GetUniqueFilePath(outFolderRoot, $"{safeName}-{timestamp}", ".html");

            var list = (results ?? Enumerable.Empty<TestResult>()).ToList();
            if (sanitize != null)
                list = AI.Sanitization.ReportSanitizer.SanitizeResults(list, sanitize).ToList();
            var jsonOptions = CreateJsonOptions();
            var rows        = list.Select((r, idx) => BuildRow(r, idx)).ToList();
            var (_, runsJson) = ComputeRunData(list, jsonOptions);

            // Embed rows as an inline JS assignment.
            // Replace </script occurrences to prevent premature tag closure.
            var dataJson = JsonSerializer.Serialize(rows, jsonOptions)
                .Replace("</script", @"<\/script", StringComparison.OrdinalIgnoreCase);

            var inlineInit =
                $"ALL_ROWS = {dataJson};\n" +
                "document.getElementById('load-msg').style.display = 'none';\n" +
                "populateDropdowns();\n" +
                "applyFilters();\n" +
                "renderStats(ALL_ROWS);";

            File.WriteAllText(
                outPath,
                BuildHtml(inlineInit, runsJson, reportName, safeName),
                Encoding.UTF8);

            return Path.GetFullPath(outPath);
        }

        // ── Internal methods used by ConsolidatedReportBuilder ───────────────────
        // These mark the output with reportType="consolidated" so subsequent scans
        // can detect and skip already-consolidated reports to prevent double-counting.

        internal static string ExportConsolidated(
            IEnumerable<TestResult> results, string outFolderRoot, string? reportName)
        {
            const string reportType = "consolidated";
            if (string.IsNullOrWhiteSpace(outFolderRoot))
                throw new ArgumentNullException(nameof(outFolderRoot));
            ValidateWebUrlMode();

            Directory.CreateDirectory(outFolderRoot);
            var safeName     = string.IsNullOrWhiteSpace(reportName) ? "test-report" : MakeSafeFileName(reportName!);
            var timestamp    = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var reportFolder = GetUniqueFolderPath(outFolderRoot, $"{safeName}-{timestamp}");
            Directory.CreateDirectory(reportFolder);

            var list = (results ?? Enumerable.Empty<TestResult>()).ToList();
            var jsonOptions = CreateJsonOptions();
            var rows        = list.Select((r, idx) => BuildRow(r, idx)).ToList();
            var (runGroups, runsJson) = ComputeRunData(list, jsonOptions);

            File.WriteAllText(Path.Combine(reportFolder, "data.json"),
                JsonSerializer.Serialize(rows, jsonOptions), Encoding.UTF8);
            File.WriteAllText(Path.Combine(reportFolder, "manifest.json"),
                JsonSerializer.Serialize(BuildManifest(list, reportName, safeName, runGroups, reportType), jsonOptions),
                Encoding.UTF8);
            File.WriteAllText(Path.Combine(reportFolder, "index.html"),
                BuildHtml(FetchInitScript, runsJson, reportName, safeName, reportType), Encoding.UTF8);

            if (SimpleSeleniumSupportDefaults.AnalyticsEnabled)
                HistoryStore.Append(list, reportName ?? "Test Run");

            return Path.GetFullPath(reportFolder);
        }

        internal static string ExportSingleFileConsolidated(
            IEnumerable<TestResult> results, string outFolderRoot, string? reportName)
        {
            const string reportType = "consolidated";
            if (string.IsNullOrWhiteSpace(outFolderRoot))
                throw new ArgumentNullException(nameof(outFolderRoot));
            ValidateWebUrlMode();

            Directory.CreateDirectory(outFolderRoot);
            var safeName  = string.IsNullOrWhiteSpace(reportName) ? "test-report" : MakeSafeFileName(reportName!);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var outPath   = GetUniqueFilePath(outFolderRoot, $"{safeName}-{timestamp}", ".html");

            var list = (results ?? Enumerable.Empty<TestResult>()).ToList();
            var jsonOptions = CreateJsonOptions();
            var rows        = list.Select((r, idx) => BuildRow(r, idx)).ToList();
            var (_, runsJson) = ComputeRunData(list, jsonOptions);

            var dataJson = JsonSerializer.Serialize(rows, jsonOptions)
                .Replace("</script", @"<\/script", StringComparison.OrdinalIgnoreCase);

            var inlineInit =
                $"ALL_ROWS = {dataJson};\n" +
                "document.getElementById('load-msg').style.display = 'none';\n" +
                "populateDropdowns();\n" +
                "applyFilters();\n" +
                "renderStats(ALL_ROWS);";

            File.WriteAllText(outPath, BuildHtml(inlineInit, runsJson, reportName, safeName, reportType), Encoding.UTF8);
            return Path.GetFullPath(outPath);
        }

        /// <summary>
        /// Exports <paramref name="results"/> to an Excel <c>.xlsx</c> workbook — one row per test result.
        /// Columns: Run · Test Name · Suite · Category · Tags · Status · Duration · Start Time ·
        /// Browser · Environment · Machine · Exception · Stack Trace · AI Analysis · Custom Properties · artifacts.
        /// </summary>
        /// <param name="results">Test results to include.</param>
        /// <param name="outFolderRoot">Directory where the <c>.xlsx</c> file is written.</param>
        /// <param name="reportName">Used to name the output file.</param>
        /// <returns>Absolute path of the generated <c>.xlsx</c> file.</returns>
        public static string ExportToExcel(
            IEnumerable<TestResult> results,
            string outFolderRoot,
            string? reportName = null,
            AI.Sanitization.SanitizationOptions? sanitize = null)
        {
            if (string.IsNullOrWhiteSpace(outFolderRoot))
                throw new ArgumentNullException(nameof(outFolderRoot));

            Directory.CreateDirectory(outFolderRoot);

            var safeName  = string.IsNullOrWhiteSpace(reportName) ? "test-report" : MakeSafeFileName(reportName!);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var outPath   = GetUniqueFilePath(outFolderRoot, $"{safeName}-{timestamp}", ".xlsx");

            var sanitized = sanitize != null
                ? AI.Sanitization.ReportSanitizer.SanitizeResults(results, sanitize)
                : results;
            ExcelExporter.ExportTestResultsToExcel(sanitized, outPath);

            return Path.GetFullPath(outPath);
        }

        // ── CI/CD XML export overloads ────────────────────────────────────────

        /// <summary>
        /// Exports <paramref name="results"/> to a JUnit 4 XML file (compatible with Jenkins,
        /// GitLab CI, GitHub Actions test reports).
        /// </summary>
        /// <param name="results">Test results to include.</param>
        /// <param name="outFolderRoot">Directory where the <c>.xml</c> file is written.</param>
        /// <param name="reportName">Used to name the output file and as the suite name attribute.</param>
        /// <returns>Absolute path of the generated XML file.</returns>
        public static string ExportToJUnit(
            IEnumerable<TestResult> results,
            string outFolderRoot,
            string? reportName = null)
        {
            Directory.CreateDirectory(outFolderRoot);
            var safeName  = string.IsNullOrWhiteSpace(reportName) ? "test-report" : MakeSafeFileName(reportName!);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var outPath   = GetUniqueFilePath(outFolderRoot, $"{safeName}-{timestamp}-junit", ".xml");
            JUnitXmlExporter.Export(results, outPath, reportName ?? "TestRun");
            return Path.GetFullPath(outPath);
        }

        /// <summary>
        /// Exports <paramref name="results"/> to an NUnit 3 XML file (compatible with
        /// Azure DevOps and TeamCity).
        /// </summary>
        /// <param name="results">Test results to include.</param>
        /// <param name="outFolderRoot">Directory where the <c>.xml</c> file is written.</param>
        /// <param name="reportName">Used to name the output file.</param>
        /// <returns>Absolute path of the generated XML file.</returns>
        public static string ExportToNUnitXml(
            IEnumerable<TestResult> results,
            string outFolderRoot,
            string? reportName = null)
        {
            Directory.CreateDirectory(outFolderRoot);
            var safeName  = string.IsNullOrWhiteSpace(reportName) ? "test-report" : MakeSafeFileName(reportName!);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var outPath   = GetUniqueFilePath(outFolderRoot, $"{safeName}-{timestamp}-nunit", ".xml");
            NUnitXmlExporter.Export(results, outPath, reportName ?? "Test Run");
            return Path.GetFullPath(outPath);
        }

        /// <summary>
        /// Exports <paramref name="results"/> to a Visual Studio TRX file (compatible with
        /// Azure DevOps test result publishing and <c>dotnet test</c>).
        /// </summary>
        /// <param name="results">Test results to include.</param>
        /// <param name="outFolderRoot">Directory where the <c>.trx</c> file is written.</param>
        /// <param name="reportName">Used to name the output file and the run name attribute.</param>
        /// <returns>Absolute path of the generated TRX file.</returns>
        public static string ExportToTrx(
            IEnumerable<TestResult> results,
            string outFolderRoot,
            string? reportName = null)
        {
            Directory.CreateDirectory(outFolderRoot);
            var safeName  = string.IsNullOrWhiteSpace(reportName) ? "test-report" : MakeSafeFileName(reportName!);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            var outPath   = GetUniqueFilePath(outFolderRoot, $"{safeName}-{timestamp}", ".trx");
            TrxExporter.Export(results, outPath, reportName ?? "Test Run");
            return Path.GetFullPath(outPath);
        }

        // ── Shared private helpers ─────────────────────────────────────────────

        private static JsonSerializerOptions CreateJsonOptions() => new JsonSerializerOptions
        {
            WriteIndented        = false,
            Encoder              = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static (object runGroups, string runsJson) ComputeRunData(
            List<TestResult> list, JsonSerializerOptions opts)
        {
            var groups = list
                .Where(r => !string.IsNullOrEmpty(r.RunName))
                .GroupBy(r => r.RunName!)
                .Select(g => new {
                    runName = g.Key,
                    total   = g.Count(),
                    pass    = g.Count(r => r.Status == TestStatus.Pass),
                    fail    = g.Count(r => r.Status == TestStatus.Fail),
                    skip    = g.Count(r => r.Status == TestStatus.Skip),
                    error   = g.Count(r => r.Status == TestStatus.Error)
                })
                .ToList<object>();
            return (groups, JsonSerializer.Serialize(groups, opts));
        }

        private static object BuildManifest(
            List<TestResult> list, string? reportName, string safeName, object runGroups,
            string reportType = "single") => new
        {
            reportName   = reportName ?? safeName,
            reportType,
            totalTests   = list.Count,
            passed       = list.Count(x => x.Status == TestStatus.Pass),
            failed       = list.Count(x => x.Status == TestStatus.Fail),
            skipped      = list.Count(x => x.Status == TestStatus.Skip),
            errors       = list.Count(x => x.Status == TestStatus.Error),
            generatedUtc = DateTime.UtcNow.ToString("o"),
            runs         = runGroups
        };

        private static string BuildHtml(
            string dataInitScript, string runsJson, string? reportName, string safeName,
            string reportType = "single")
        {
            // In WebUrl mode inject the configured base URL; Local mode leaves it empty
            // so toFileUrl() in the browser falls back to the file:/// path.
            var baseUrl = SimpleSeleniumSupportDefaults.ArtifactLinkMode == ArtifactLinkMode.WebUrl
                ? (SimpleSeleniumSupportDefaults.ReportBaseUrl ?? "").TrimEnd('/')
                : "";

            return GetTemplate()
                .Replace("[[REPORT_NAME]]",      HtmlEnc(reportName ?? safeName))
                .Replace("[[GEN_TIME]]",          DateTime.UtcNow.ToString("u"))
                .Replace("[[RUNS_JSON]]",         runsJson.Replace("</script", @"<\/script", StringComparison.OrdinalIgnoreCase))
                .Replace("[[REPORT_TYPE]]",       reportType)
                .Replace("[[REPORT_BASE_URL]]",   baseUrl)
                .Replace("[[DATA_INIT_SCRIPT]]",  dataInitScript);
        }

        private const string FetchInitScript =
            "fetch('data.json')\n" +
            "  .then(r => r.json())\n" +
            "  .then(rows => {\n" +
            "    ALL_ROWS = rows;\n" +
            "    document.getElementById('load-msg').style.display = 'none';\n" +
            "    populateDropdowns();\n" +
            "    applyFilters();\n" +
            "    renderStats(ALL_ROWS);\n" +
            "  })\n" +
            "  .catch(e => {\n" +
            "    document.getElementById('load-msg').textContent = 'Error loading data.json: ' + e.message;\n" +
            "  });";

        // ── Row serialisation ──────────────────────────────────────────────────

        private static object BuildRow(TestResult r, int idx) => new
        {
            __index        = idx,
            runName        = r.RunName        ?? "",
            testName       = r.TestName,
            testSuite      = r.TestSuite      ?? "",
            fullName       = r.FullName       ?? "",
            category       = r.Category       ?? "",
            tags           = r.Tags           ?? "",
            status         = r.Status.ToString(),
            durationMs     = (long)r.DurationMs,
            startTime      = r.StartTime.ToString("o"),
            browser        = r.Browser        ?? "",
            environment    = r.Environment    ?? "",
            machineName    = r.MachineName    ?? "",
            exceptionType  = r.ExceptionType  ?? "",
            exceptionMsg   = r.ExceptionMessage ?? "",
            stackTrace     = r.StackTrace     ?? "",
            assertMsg      = r.AssertMessage  ?? "",
            skipReason     = r.SkipReason     ?? "",
            screenshot     = ArtifactPath(r.ScreenshotPath),
            screencast     = ArtifactPath(r.ScreencastPath),
            diagnostics    = ArtifactPath(r.DiagnosticsFolder),
            networkHar     = ArtifactPath(r.NetworkHarPath),
            networkExcel   = ArtifactPath(r.NetworkExcelPath),
            aiAnalysis       = r.AiAnalysis       ?? "",
            aiClassification = r.AiClassification ?? "",
            aiConfidence     = r.AiConfidence     ?? "",
            customProps      = r.CustomProperties
        };

        // ── Utilities ──────────────────────────────────────────────────────────

        // Resolves an artifact path based on the configured ArtifactLinkMode.
        //
        // Local mode  → absolute Windows path   (browser builds file:/// link)
        // WebUrl mode → path relative to ReportRootDirectory using forward slashes
        //               (browser prepends REPORT_BASE_URL to form a web URL)
        //
        // Paths that are already HTTP/HTTPS/file:// URLs are passed through unchanged.
        // WebUrl mode: paths outside ReportRootDirectory fall back to absolute with a warning.
        private static string ArtifactPath(string? p)
        {
            if (string.IsNullOrEmpty(p)) return "";

            // Already a full URL — pass through unchanged
            if (p.StartsWith("http://",  StringComparison.OrdinalIgnoreCase) ||
                p.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                p.StartsWith("file://",  StringComparison.OrdinalIgnoreCase))
                return p;

            var abs = Path.GetFullPath(p);

            if (SimpleSeleniumSupportDefaults.ArtifactLinkMode == ArtifactLinkMode.WebUrl)
            {
                var root = SimpleSeleniumSupportDefaults.ReportRootDirectory;
                if (!string.IsNullOrEmpty(root))
                {
                    // Normalize root: ensure trailing separator so the substring is clean
                    var fullRoot = Path.GetFullPath(root).TrimEnd('\\', '/')
                                   + Path.DirectorySeparatorChar;

                    if (abs.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                        return abs.Substring(fullRoot.Length).Replace('\\', '/');
                }

                // Artifact is outside the configured root — warn and fall back to absolute.
                // toFileUrl() in the browser will produce a file:// link for this path,
                // which still works locally but won't be accessible from other machines.
                _log.LogWarning(
                    "[Report] Artifact '{Path}' is outside ReportRootDirectory '{Root}'. " +
                    "Falling back to absolute path. Move the file inside the root directory " +
                    "or update SimpleSeleniumSupportDefaults.ReportRootDirectory.",
                    abs, root);
            }

            return abs;
        }

        // Validates that all required settings are present when WebUrl mode is active.
        // Called once at the start of every export method to fail fast with a clear message.
        private static void ValidateWebUrlMode()
        {
            if (SimpleSeleniumSupportDefaults.ArtifactLinkMode != ArtifactLinkMode.WebUrl)
                return;

            if (string.IsNullOrWhiteSpace(SimpleSeleniumSupportDefaults.ReportRootDirectory))
                throw new InvalidOperationException(
                    "SimpleSeleniumSupportDefaults.ReportRootDirectory must be set when " +
                    "ArtifactLinkMode is WebUrl. Set it to the directory your web server " +
                    @"serves as its root (e.g. @""D:\wwwroot\"").");

            var baseUrl = SimpleSeleniumSupportDefaults.ReportBaseUrl ?? "";
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new InvalidOperationException(
                    "SimpleSeleniumSupportDefaults.ReportBaseUrl must be set when " +
                    "ArtifactLinkMode is WebUrl. Set it to the public base URL of your " +
                    "site (e.g. \"https://reports.mycompany.com\").");

            if (!baseUrl.StartsWith("http://",  StringComparison.OrdinalIgnoreCase) &&
                !baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    "SimpleSeleniumSupportDefaults.ReportBaseUrl must start with " +
                    $"http:// or https://. Got: \"{baseUrl}\"");
        }

        private static string HtmlEnc(string s) => System.Net.WebUtility.HtmlEncode(s);

        private static string MakeSafeFileName(string input)
        {
            foreach (var c in Path.GetInvalidFileNameChars()) input = input.Replace(c, '-');
            var s = string.Join("-", input.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
            while (s.Contains("--")) s = s.Replace("--", "-");
            return s.Trim('-');
        }

        private static string GetUniqueFolderPath(string root, string baseName)
        {
            string candidate = Path.Combine(root, baseName);
            if (!Directory.Exists(candidate)) return candidate;
            for (int i = 1; i < 1000; i++)
            {
                var alt = Path.Combine(root, $"{baseName}-{i}");
                if (!Directory.Exists(alt)) return alt;
            }
            return Path.Combine(root, $"{baseName}-{Guid.NewGuid():N}");
        }

        // Guards same-second file-name collisions when parallel tests all export at once
        private static readonly object _filePathLock = new();

        private static string GetUniqueFilePath(string root, string baseName, string ext)
        {
            lock (_filePathLock)
            {
                string candidate = Path.Combine(root, $"{baseName}{ext}");
                if (!File.Exists(candidate)) return candidate;
                for (int i = 1; i < 1000; i++)
                {
                    var alt = Path.Combine(root, $"{baseName}-{i}{ext}");
                    if (!File.Exists(alt)) return alt;
                }
                return Path.Combine(root, $"{baseName}-{Guid.NewGuid():N}{ext}");
            }
        }

        // ── HTML template — assembled from CSS, HTML structure, and JavaScript ─

        private static string GetTemplate()
        {
            return GetHtmlStructure()
                .Replace("[[CSS_STYLES]]", GetCssStyles())
                .Replace("[[JAVASCRIPT]]", GetJavaScript());
        }

        // ── CSS styles (no style tags) ─────────────────────────────────────────

        private static string GetCssStyles() => """
/* ── CSS Variables ─────────────────────────────────────────────────── */
:root {
  --hdr:   #0f172a;
  --hdr2:  #1e293b;
  --accent:#3b82f6;
  --bg:    #f1f5f9;
  --card:  #fff;
  --border:#e2e8f0;
  --text:  #1e293b;
  --muted: #64748b;
  --color-pass: #16a34a;
  --color-fail: #dc2626;
  --color-error:#ea580c;
  --color-skip: #6b7280;
  --row-pass:  #f0fdf4;
  --row-fail:  #fff5f5;
  --row-error: #fff7ed;
  --row-skip:  #f9fafb;
  --shadow: 0 1px 3px rgba(0,0,0,.08);
}
/* ── Reset / base ──────────────────────────────────────────────────── */
*, *::before, *::after { box-sizing: border-box; }
body {
  margin: 0;
  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif;
  font-size: 13px;
  background: var(--bg);
  color: var(--text);
}
/* ── Dark mode overrides ─────────────────────────────────────────────── */
html[data-theme=dark] {
  --hdr:   #0d1117;
  --hdr2:  #161b22;
  --accent:#60a5fa;
  --bg:    #0f172a;
  --card:  #1e293b;
  --border:#334155;
  --text:  #e2e8f0;
  --muted: #94a3b8;
  --color-pass: #22c55e;
  --color-fail: #ef4444;
  --color-error:#f97316;
  --color-skip: #9ca3af;
  --row-pass:  #052e16;
  --row-fail:  #2d0a0a;
  --row-error: #2d1400;
  --row-skip:  #1a1f2e;
  --shadow: 0 1px 3px rgba(0,0,0,.4);
}
/* ── App shell ────────────────────────────────────────────────────────── */
.app { display: flex; flex-direction: column; min-height: 100vh; }
/* ── Header ──────────────────────────────────────────────────────────── */
.hdr {
  background: var(--hdr);
  color: #f1f5f9;
  padding: 0 20px;
  display: flex;
  align-items: center;
  gap: 16px;
  height: 56px;
  position: sticky;
  top: 0;
  z-index: 300;
  box-shadow: 0 2px 8px rgba(0,0,0,.3);
  flex-shrink: 0;
}
.hdr-left { display: flex; flex-direction: column; gap: 1px; }
.hdr-title {
  font-size: 15px;
  font-weight: 700;
  color: #f1f5f9;
  margin: 0;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 400px;
}
.hdr-meta { font-size: 11px; color: #94a3b8; }
.hdr-right { margin-left: auto; display: flex; align-items: center; gap: 10px; }
#donut { cursor: default; }
.theme-btn {
  background: none;
  border: 1px solid #475569;
  color: #94a3b8;
  border-radius: 6px;
  width: 32px;
  height: 32px;
  cursor: pointer;
  font-size: 15px;
  display: flex;
  align-items: center;
  justify-content: center;
  transition: .15s;
}
.theme-btn:hover { border-color: #94a3b8; color: #e2e8f0; }
/* ── KPI row ──────────────────────────────────────────────────────────── */
.kpi-row {
  display: flex;
  gap: 10px;
  padding: 12px 20px 0;
  flex-wrap: wrap;
}
.kpi {
  background: var(--card);
  border: 1px solid var(--border);
  border-radius: 10px;
  padding: 10px 18px;
  text-align: center;
  min-width: 90px;
  box-shadow: var(--shadow);
  transition: transform .12s;
}
.kpi:hover { transform: translateY(-1px); }
.kpi-val {
  font-size: 22px;
  font-weight: 800;
  line-height: 1.15;
  color: var(--text);
}
.kpi-lbl {
  font-size: 10px;
  color: var(--muted);
  text-transform: uppercase;
  letter-spacing: .5px;
  margin-top: 1px;
}
.kpi.pass  .kpi-val { color: var(--color-pass);  }
.kpi.fail  .kpi-val { color: var(--color-fail);  }
.kpi.error .kpi-val { color: var(--color-error); }
.kpi.skip  .kpi-val { color: var(--color-skip);  }
/* ── Pass-rate bar ────────────────────────────────────────────────────── */
.pass-bar-wrap {
  margin: 10px 20px 0;
  height: 6px;
  background: var(--border);
  border-radius: 3px;
  overflow: hidden;
}
.pass-bar {
  height: 100%;
  border-radius: 3px;
  width: 0;
  transition: width .6s ease, background-color .4s;
}
/* ── Tabs ─────────────────────────────────────────────────────────────── */
.tabs {
  display: flex;
  gap: 2px;
  padding: 12px 20px 0;
  border-bottom: 2px solid var(--border);
}
.tab-btn {
  background: none;
  border: none;
  border-bottom: 3px solid transparent;
  margin-bottom: -2px;
  padding: 7px 16px 9px;
  font-size: 13px;
  font-weight: 600;
  color: var(--muted);
  cursor: pointer;
  transition: color .12s, border-color .12s;
}
.tab-btn:hover { color: var(--text); }
.tab-btn.active { color: var(--accent); border-bottom-color: var(--accent); }
.tab-panel { display: none; }
.tab-panel.active { display: block; }
/* ── Runs bar ─────────────────────────────────────────────────────────── */
#runsBar {
  display: none;
  padding: 10px 20px;
  background: var(--card);
  border-bottom: 1px solid var(--border);
  overflow-x: auto;
}
.runs-scroll { display: flex; gap: 8px; min-width: max-content; }
.run-card {
  background: var(--bg);
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 8px 14px;
  min-width: 150px;
  cursor: pointer;
  transition: .15s;
  user-select: none;
}
.run-card:hover  { border-color: #94a3b8; }
.run-card.active { border-color: var(--accent); background: var(--card); }
.run-name {
  font-weight: 700;
  font-size: 12px;
  color: var(--text);
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 200px;
}
.run-stats { font-size: 11px; margin-top: 3px; display: flex; gap: 6px; flex-wrap: wrap; }
.run-pass-rate { font-size: 10px; color: var(--muted); margin-top: 3px; text-align: right; }
.run-progress-bar { height: 3px; border-radius: 2px; background: var(--border); margin-top: 3px; }
.run-progress-fill { height: 3px; border-radius: 2px; }
/* ── Filter bar ───────────────────────────────────────────────────────── */
.filter-bar {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  padding: 10px 20px;
  background: var(--card);
  border-bottom: 1px solid var(--border);
}
.pill-group { display: flex; gap: 3px; }
.pill {
  border: 1px solid var(--border);
  background: var(--bg);
  color: var(--text);
  border-radius: 20px;
  padding: 3px 12px;
  font-size: 11px;
  font-weight: 600;
  cursor: pointer;
  transition: .12s;
  white-space: nowrap;
}
.pill:hover { border-color: #94a3b8; }
.pill.active { background: var(--hdr2); color: #fff; border-color: var(--hdr2); }
.pill.p-pass.active  { background: var(--color-pass);  border-color: var(--color-pass); }
.pill.p-fail.active  { background: var(--color-fail);  border-color: var(--color-fail); }
.pill.p-error.active { background: var(--color-error); border-color: var(--color-error); }
.pill.p-skip.active  { background: var(--color-skip);  border-color: var(--color-skip); }
.f-select {
  border: 1px solid var(--border);
  background: var(--bg);
  color: var(--text);
  border-radius: 6px;
  padding: 4px 8px;
  font-size: 12px;
  min-width: 110px;
  height: 28px;
}
.f-search {
  border: 1px solid var(--border);
  background: var(--bg);
  color: var(--text);
  border-radius: 6px;
  padding: 4px 10px;
  font-size: 12px;
  width: 200px;
  height: 28px;
  outline: none;
}
.f-search:focus { border-color: var(--accent); box-shadow: 0 0 0 2px rgba(59,130,246,.2); }
.f-clear-btn {
  border: 1px solid var(--border);
  background: var(--bg);
  color: var(--muted);
  border-radius: 6px;
  padding: 3px 12px;
  font-size: 12px;
  cursor: pointer;
  height: 28px;
  transition: .12s;
}
.f-clear-btn:hover { border-color: #94a3b8; color: var(--text); }
/* ── Table ────────────────────────────────────────────────────────────── */
.tbl-wrap { padding: 16px 20px 8px; overflow-x: auto; }
.results-table {
  width: 100%;
  border-collapse: collapse;
  background: var(--card);
  border-radius: 10px;
  overflow: hidden;
  box-shadow: var(--shadow);
  border: 1px solid var(--border);
}
.results-table th {
  background: var(--hdr2);
  color: #e2e8f0;
  padding: 9px 12px;
  text-align: left;
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: .4px;
  white-space: nowrap;
  user-select: none;
  cursor: pointer;
}
.results-table th:hover { background: #273549; }
.results-table th.sort-asc::after  { content: ' \25B2'; font-size: 9px; }
.results-table th.sort-desc::after { content: ' \25BC'; font-size: 9px; }
.results-table td {
  padding: 8px 12px;
  border-top: 1px solid var(--border);
  font-size: 12px;
  vertical-align: middle;
}
.results-table tr.row-pass  td { background: var(--row-pass); }
.results-table tr.row-fail  td { background: var(--row-fail); }
.results-table tr.row-error td { background: var(--row-error); }
.results-table tr.row-skip  td { background: var(--row-skip); }
.results-table tbody tr:hover td { filter: brightness(.97); }
.cell-name {
  font-weight: 600;
  color: var(--text);
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  max-width: 260px;
  cursor: pointer;
  word-break: break-word;
}
.cell-name:hover { color: var(--accent); text-decoration: underline; }
.cell-suite { color: var(--muted); font-size: 11px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 160px; }
.cell-dur { color: var(--muted); font-size: 11px; white-space: nowrap; }
.cell-info { max-width: 180px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: var(--muted); font-size: 11px; }
.cell-flak { font-size: 11px; white-space: nowrap; }
/* ── Status badges ─────────────────────────────────────────────────────── */
.badge {
  display: inline-block;
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 11px;
  font-weight: 700;
  white-space: nowrap;
}
.badge-pass  { background: var(--color-pass);  color: #fff; }
.badge-fail  { background: var(--color-fail);  color: #fff; }
.badge-error { background: var(--color-error); color: #fff; }
.badge-skip  { background: var(--color-skip);  color: #fff; }
/* ── Details button ──────────────────────────────────────────────────── */
.details-btn {
  background: var(--bg);
  border: 1px solid var(--border);
  color: var(--accent);
  border-radius: 5px;
  padding: 2px 10px;
  font-size: 11px;
  cursor: pointer;
  white-space: nowrap;
  transition: .12s;
}
.details-btn:hover { background: var(--accent); color: #fff; border-color: var(--accent); }
/* ── Cards view ────────────────────────────────────────────────────────── */
.cards-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 12px;
  padding: 16px 20px;
}
.result-card {
  background: var(--card);
  border: 1px solid var(--border);
  border-left: 4px solid var(--border);
  border-radius: 8px;
  padding: 12px 14px;
  box-shadow: var(--shadow);
  cursor: pointer;
  transition: transform .12s, box-shadow .12s;
}
.result-card:hover { transform: translateY(-2px); box-shadow: 0 4px 16px rgba(0,0,0,.12); }
.result-card.st-Pass  { border-left-color: var(--color-pass); }
.result-card.st-Fail  { border-left-color: var(--color-fail); }
.result-card.st-Error { border-left-color: var(--color-error); }
.result-card.st-Skip  { border-left-color: var(--color-skip); }
.card-name {
  font-weight: 600;
  font-size: 13px;
  color: var(--text);
  display: -webkit-box;
  -webkit-line-clamp: 2;
  -webkit-box-orient: vertical;
  overflow: hidden;
  margin-bottom: 4px;
}
.card-suite { font-size: 11px; color: var(--muted); margin-bottom: 6px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
.card-meta { display: flex; gap: 6px; align-items: center; flex-wrap: wrap; }
/* ── Pagination ─────────────────────────────────────────────────────── */
.pagination {
  display: flex;
  align-items: center;
  gap: 4px;
  justify-content: center;
  padding: 12px 20px 16px;
  flex-wrap: wrap;
}
.pg-btn {
  min-width: 30px;
  height: 30px;
  padding: 0 8px;
  border: 1px solid var(--border);
  background: var(--card);
  color: var(--text);
  border-radius: 6px;
  font-size: 12px;
  cursor: pointer;
  transition: .12s;
}
.pg-btn:hover:not(:disabled) { background: var(--accent); color: #fff; border-color: var(--accent); }
.pg-btn.active { background: var(--accent); color: #fff; border-color: var(--accent); font-weight: 700; }
.pg-btn:disabled { opacity: .4; cursor: default; }
.pg-ellipsis { font-size: 13px; color: var(--muted); padding: 0 4px; }
.pg-info { font-size: 11px; color: var(--muted); margin-left: 8px; }
/* ── AI tab table ─────────────────────────────────────────────────────── */
.ai-table {
  width: 100%;
  border-collapse: collapse;
  background: var(--card);
  border-radius: 10px;
  overflow: hidden;
  box-shadow: var(--shadow);
  border: 1px solid var(--border);
}
.ai-table th {
  background: var(--hdr2);
  color: #e2e8f0;
  padding: 9px 12px;
  text-align: left;
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
  letter-spacing: .4px;
  white-space: nowrap;
}
.ai-table td {
  padding: 8px 12px;
  border-top: 1px solid var(--border);
  font-size: 12px;
  vertical-align: middle;
}
.ai-snippet { max-width: 300px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; color: var(--muted); }
/* ── Trends table ────────────────────────────────────────────────────── */
.trends-table {
  width: 100%;
  border-collapse: collapse;
  background: var(--card);
  border-radius: 10px;
  overflow: hidden;
  box-shadow: var(--shadow);
  border: 1px solid var(--border);
}
.trends-table th {
  background: var(--hdr2);
  color: #e2e8f0;
  padding: 9px 12px;
  text-align: left;
  font-size: 11px;
  font-weight: 700;
  text-transform: uppercase;
}
.trends-table td { padding: 8px 12px; border-top: 1px solid var(--border); font-size: 12px; }
.trend-bar-wrap { display: flex; align-items: center; gap: 8px; }
.trend-bar-bg { flex: 1; height: 8px; background: var(--border); border-radius: 4px; overflow: hidden; }
.trend-bar-fill { height: 8px; border-radius: 4px; }
/* ── AI badges ─────────────────────────────────────────────────────────── */
.badge-ai {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  padding: 2px 8px;
  border-radius: 4px;
  font-size: 11px;
  font-weight: 700;
  white-space: nowrap;
}
.badge-ai-testissue      { background: #dbeafe; color: #1e40af; }
.badge-ai-productissue   { background: #fee2e2; color: #991b1b; }
.badge-ai-flaky          { background: #fef3c7; color: #92400e; }
.badge-ai-infrastructure { background: #e0e7ff; color: #3730a3; }
.badge-ai-uncertain      { background: #f1f5f9; color: #475569; }
html[data-theme=dark] .badge-ai-testissue      { background: #1e3a5f; color: #93c5fd; }
html[data-theme=dark] .badge-ai-productissue   { background: #3b1a1a; color: #fca5a5; }
html[data-theme=dark] .badge-ai-flaky          { background: #3b2f0a; color: #fde68a; }
html[data-theme=dark] .badge-ai-infrastructure { background: #2a2550; color: #a5b4fc; }
html[data-theme=dark] .badge-ai-uncertain      { background: #1e293b; color: #94a3b8; }
.conf-dot { font-size: 9px; }
/* ── AI analysis panel ─────────────────────────────────────────────── */
.ai-analysis-panel {
  background: #f0f9ff;
  border-left: 4px solid #0ea5e9;
  border-radius: 0 6px 6px 0;
  padding: 12px 16px;
  font-size: 13px;
  white-space: pre-wrap;
  word-break: break-word;
  line-height: 1.6;
  color: var(--text);
}
html[data-theme=dark] .ai-analysis-panel { background: #0c1a2e; border-left-color: #38bdf8; }
.xpath-candidate {
  background: #fef3c7;
  border: 1px solid #fbbf24;
  border-radius: 3px;
  padding: 0 4px;
  font-family: monospace;
  font-size: 12px;
}
/* ── Modal overlay ─────────────────────────────────────────────────── */
.modal-overlay {
  display: none;
  position: fixed;
  inset: 0;
  background: rgba(0,0,0,.55);
  z-index: 400;
  align-items: flex-start;
  justify-content: center;
  padding: 40px 20px;
  overflow-y: auto;
}
.modal-overlay.open { display: flex; }
.modal-box {
  background: var(--card);
  border-radius: 12px;
  width: 100%;
  max-width: 820px;
  box-shadow: 0 20px 60px rgba(0,0,0,.4);
  flex-shrink: 0;
  margin: auto;
}
.modal-hdr {
  background: var(--hdr);
  color: #f1f5f9;
  padding: 14px 20px;
  border-radius: 12px 12px 0 0;
  display: flex;
  align-items: flex-start;
  gap: 12px;
}
.modal-title {
  font-size: 14px;
  font-weight: 700;
  flex: 1;
  word-break: break-word;
  margin: 0;
  line-height: 1.4;
}
.modal-close {
  background: none;
  border: none;
  color: #94a3b8;
  font-size: 20px;
  cursor: pointer;
  padding: 0;
  line-height: 1;
  flex-shrink: 0;
}
.modal-close:hover { color: #e2e8f0; }
.modal-tabs {
  display: flex;
  gap: 2px;
  padding: 10px 16px 0;
  border-bottom: 1px solid var(--border);
  background: var(--card);
  overflow-x: auto;
}
.modal-tab-btn {
  background: none;
  border: none;
  border-bottom: 2px solid transparent;
  margin-bottom: -1px;
  padding: 6px 14px 8px;
  font-size: 12px;
  font-weight: 600;
  color: var(--muted);
  cursor: pointer;
  white-space: nowrap;
  transition: .12s;
}
.modal-tab-btn:hover { color: var(--text); }
.modal-tab-btn.active { color: var(--accent); border-bottom-color: var(--accent); }
.modal-body { max-height: 70vh; overflow-y: auto; }
.modal-pane { display: none; padding: 16px 20px; }
.modal-pane.active { display: block; }
.info-table { width: 100%; border-collapse: collapse; font-size: 12px; }
.info-table tr + tr td, .info-table tr + tr th { border-top: 1px solid var(--border); }
.info-table th { width: 130px; padding: 6px 10px 6px 0; color: var(--muted); font-weight: 600; vertical-align: top; white-space: nowrap; }
.info-table td { padding: 6px 0; word-break: break-word; }
.fail-exc-box {
  background: #fef2f2;
  border-left: 3px solid var(--color-fail);
  border-radius: 0 6px 6px 0;
  padding: 10px 14px;
  font-family: monospace;
  font-size: 12px;
  margin-bottom: 12px;
  word-break: break-word;
  color: #7f1d1d;
}
html[data-theme=dark] .fail-exc-box { background: #2d0a0a; color: #fca5a5; }
.assert-box {
  background: #fffbeb;
  border-left: 3px solid #fbbf24;
  border-radius: 0 6px 6px 0;
  padding: 8px 12px;
  font-size: 12px;
  margin-bottom: 12px;
}
html[data-theme=dark] .assert-box { background: #2d2000; }
.stack-wrap { position: relative; margin-top: 4px; }
pre.stack-pre {
  background: #0f172a;
  color: #e2e8f0;
  border-radius: 6px;
  padding: 14px;
  font-size: 11px;
  max-height: 280px;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-word;
  margin: 0;
}
.copy-stack-btn {
  position: absolute;
  top: 6px;
  right: 6px;
  background: #334155;
  border: none;
  color: #94a3b8;
  border-radius: 4px;
  padding: 2px 8px;
  font-size: 11px;
  cursor: pointer;
}
.copy-stack-btn:hover { background: #475569; color: #f1f5f9; }
.stack-toggle {
  font-size: 11px;
  color: var(--muted);
  cursor: pointer;
  text-decoration: underline;
  display: block;
  margin-top: 5px;
  background: none;
  border: none;
  padding: 0;
}
.skip-box {
  background: #f8fafc;
  border-left: 3px solid var(--color-skip);
  border-radius: 0 6px 6px 0;
  padding: 8px 12px;
  font-size: 12px;
  color: var(--text);
}
html[data-theme=dark] .skip-box { background: var(--hdr2); }
.ss-img { max-width: 100%; border-radius: 6px; border: 1px solid var(--border); cursor: zoom-in; display: block; margin-bottom: 8px; }
.ss-img:hover { box-shadow: 0 4px 20px rgba(0,0,0,.2); }
.open-link {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--accent);
  text-decoration: none;
  border: 1px solid var(--accent);
  border-radius: 5px;
  padding: 4px 12px;
}
.open-link:hover { background: var(--accent); color: #fff; }
.modal-video { width: 100%; max-height: 380px; background: #000; border-radius: 6px; display: block; }
.art-btn {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  text-decoration: none;
  border-radius: 5px;
  padding: 5px 14px;
  border: 1px solid;
  margin-right: 8px;
  transition: .12s;
}
.art-btn-primary { color: var(--accent); border-color: var(--accent); }
.art-btn-primary:hover { background: var(--accent); color: #fff; }
.art-btn-success { color: var(--color-pass); border-color: var(--color-pass); }
.art-btn-success:hover { background: var(--color-pass); color: #fff; }
.cp-table { width: 100%; border-collapse: collapse; font-size: 12px; }
.cp-table th { background: var(--bg); padding: 6px 10px; text-align: left; font-weight: 700; border: 1px solid var(--border); color: var(--text); }
.cp-table td { padding: 6px 10px; border: 1px solid var(--border); word-break: break-word; color: var(--text); }
/* ── Lightbox ─────────────────────────────────────────────────────────── */
#lightbox {
  display: none;
  position: fixed;
  inset: 0;
  background: rgba(0,0,0,.88);
  z-index: 9999;
  align-items: center;
  justify-content: center;
}
#lightbox.open { display: flex; }
#lightbox img { max-width: 92vw; max-height: 90vh; border-radius: 6px; box-shadow: 0 8px 40px rgba(0,0,0,.6); }
#lightbox-close {
  position: absolute;
  top: 14px;
  right: 20px;
  font-size: 30px;
  color: #fff;
  cursor: pointer;
  line-height: 1;
  background: none;
  border: none;
}
/* ── Load message ─────────────────────────────────────────────────────── */
#load-msg { padding: 40px 20px; text-align: center; color: var(--muted); font-size: 14px; }
/* ── Tree view ──────────────────────────────────────────────────────────── */
#tree-view { padding: 0 20px 20px; }
.suite-block {
  border: 1px solid var(--border);
  border-radius: 8px;
  margin-bottom: 8px;
  overflow: hidden;
  background: var(--card);
}
.suite-header {
  padding: 10px 14px;
  cursor: pointer;
  background: var(--bg);
  display: flex;
  align-items: center;
  gap: 8px;
  user-select: none;
}
.suite-header:hover { background: var(--border); }
.suite-chevron { font-size: 11px; color: var(--muted); width: 14px; display: inline-block; flex-shrink: 0; text-align: center; }
.suite-name { font-weight: 600; font-size: 13px; flex: 1; color: var(--text); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.suite-meta { color: var(--muted); font-size: 11px; white-space: nowrap; flex-shrink: 0; }
.suite-counts { display: flex; gap: 3px; flex-shrink: 0; }
.suite-count { padding: 1px 8px; border-radius: 10px; font-size: 11px; font-weight: 700; }
.suite-count.pass  { background: #dcfce7; color: #166534; }
.suite-count.fail  { background: #fee2e2; color: #991b1b; }
.suite-count.error { background: #ffedd5; color: #9a3412; }
.suite-count.skip  { background: #f1f5f9; color: #475569; }
html[data-theme=dark] .suite-count.pass  { background: #052e16; color: #86efac; }
html[data-theme=dark] .suite-count.fail  { background: #2d0a0a; color: #fca5a5; }
html[data-theme=dark] .suite-count.error { background: #2d1400; color: #fdba74; }
html[data-theme=dark] .suite-count.skip  { background: #1a1f2e; color: #94a3b8; }
.suite-body { display: none; }
.suite-body.open { display: block; }
.test-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 14px 6px 28px;
  border-top: 1px solid var(--border);
  cursor: pointer;
  transition: .1s;
}
.test-row:hover { background: var(--bg); }
.test-row[data-st=Fail]  { border-left: 3px solid var(--color-fail); }
.test-row[data-st=Error] { border-left: 3px solid var(--color-error); }
.test-row[data-st=Pass]  { border-left: 3px solid var(--color-pass); }
.test-row[data-st=Skip]  { border-left: 3px solid var(--color-skip); }
.test-status-icon { width: 16px; height: 16px; border-radius: 50%; display: inline-flex; align-items: center; justify-content: center; font-size: 9px; color: #fff; flex-shrink: 0; }
.test-status-icon.Pass  { background: var(--color-pass); }
.test-status-icon.Fail  { background: var(--color-fail); }
.test-status-icon.Error { background: var(--color-error); }
.test-status-icon.Skip  { background: var(--color-skip); }
.test-name-label { flex: 1; font-size: 12px; color: var(--text); word-break: break-word; min-width: 0; }
.test-duration-label { color: var(--muted); font-size: 11px; white-space: nowrap; flex-shrink: 0; }
.test-artifacts-list { display: flex; gap: 3px; flex-shrink: 0; }
.artifact-link { padding: 1px 6px; border-radius: 3px; font-size: 10px; font-weight: 600; text-decoration: none; }
.artifact-link-screenshot { background: #eff6ff; color: #1d4ed8; }
.artifact-link-video      { background: #fdf4ff; color: #7e22ce; }
.artifact-link-har        { background: #f0fdf4; color: #15803d; }
.test-detail-panel { background: var(--bg); padding: 10px 14px 10px 28px; border-top: 1px solid var(--border); display: none; }
.test-detail-panel.open { display: block; }
.detail-tabs-row { display: flex; gap: 3px; margin-bottom: 8px; flex-wrap: wrap; }
.detail-tab-btn { border: 1px solid var(--border); background: var(--card); color: var(--text); border-radius: 4px; padding: 2px 10px; font-size: 11px; cursor: pointer; transition: .1s; }
.detail-tab-btn.active { background: var(--hdr2); color: #fff; border-color: var(--hdr2); }
.detail-tab-pane { display: none; }
.detail-tab-pane.active { display: block; }
.failure-box { background: #fef2f2; border-left: 3px solid var(--color-fail); padding: 8px 12px; border-radius: 0 4px 4px 0; font-size: 12px; font-family: monospace; margin-bottom: 8px; white-space: pre-wrap; word-break: break-word; }
html[data-theme=dark] .failure-box { background: #2d0a0a; color: #fca5a5; }
.inline-stack-box { font-size: 11px; background: #0f172a; color: #e2e8f0; border-radius: 4px; padding: 10px; max-height: 200px; overflow: auto; white-space: pre-wrap; word-break: break-word; }
.run-block { border: 1px solid var(--border); border-radius: 8px; margin-bottom: 10px; overflow: hidden; background: var(--card); }
.run-block-header { padding: 11px 14px; cursor: pointer; background: var(--hdr2); color: #f1f5f9; display: flex; align-items: center; gap: 8px; user-select: none; }
.run-block-header:hover { background: #273549; }
.run-block-chevron { font-size: 11px; color: #94a3b8; width: 14px; display: inline-block; flex-shrink: 0; text-align: center; }
.run-block-name { font-weight: 700; font-size: 13px; flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.run-block-body { display: none; padding: 8px 8px 4px; }
.run-block-body.open { display: block; }
.run-block-body .suite-block { margin-bottom: 6px; }
.run-block-body .test-row { padding-left: 42px; }
.run-block-body .test-detail-panel { padding-left: 42px; }
.run-badge { background: #e0e7ff; color: #3730a3; padding: 1px 7px; border-radius: 4px; font-size: 11px; font-weight: 600; white-space: nowrap; }
html[data-theme=dark] .run-badge { background: #2a2550; color: #a5b4fc; }
.art-icon { cursor: pointer; font-size: 13px; padding: 1px 3px; border-radius: 3px; display: inline-block; line-height: 1; transition: transform .1s; }
.art-icon:hover { transform: scale(1.25); }
.empty-state { padding: 48px 20px; text-align: center; color: var(--muted); font-size: 14px; }
.tbl-toolbar { display: flex; gap: 6px; align-items: center; padding: 10px 20px 0; flex-wrap: wrap; }
.toolbar-btn {
  background: var(--card);
  border: 1px solid var(--border);
  color: var(--text);
  border-radius: 6px;
  padding: 4px 12px;
  font-size: 12px;
  cursor: pointer;
  transition: .12s;
}
.toolbar-btn:hover { background: var(--accent); color: #fff; border-color: var(--accent); }
.view-toggle { display: flex; gap: 2px; }
.view-btn {
  background: var(--bg);
  border: 1px solid var(--border);
  color: var(--muted);
  border-radius: 5px;
  padding: 3px 10px;
  font-size: 12px;
  cursor: pointer;
  transition: .12s;
}
.view-btn.active { background: var(--accent); color: #fff; border-color: var(--accent); }
""";

        // ── HTML structure (with [[CSS_STYLES]] and [[JAVASCRIPT]] placeholders) ─

        private static string GetHtmlStructure() => """
<!doctype html>
<html lang="en" data-theme="light">
<head>
<meta charset="utf-8"/>
<meta name="viewport" content="width=device-width,initial-scale=1"/>
<meta name="generator" content="SimpleSeleniumSupport">
<meta name="sss:report-name" content="[[REPORT_NAME]]">
<meta name="sss:report-type" content="[[REPORT_TYPE]]">
<title>[[REPORT_NAME]] — Test Report</title>
<script>(function(){var t=localStorage.getItem('sss-theme');if(t)document.documentElement.setAttribute('data-theme',t);})()</script>
<style>
[[CSS_STYLES]]
</style>
</head>
<body>
<div class="app">

<header class="hdr">
  <div class="hdr-left">
    <h1 class="hdr-title">[[REPORT_NAME]]</h1>
    <span class="hdr-meta">Generated [[GEN_TIME]] &bull; <span id="hdr-count">&#8230;</span> tests</span>
  </div>
  <div class="hdr-right">
    <svg id="donut" width="48" height="48" viewBox="0 0 48 48">
      <circle cx="24" cy="24" r="20" fill="none" stroke="#334155" stroke-width="6"/>
      <circle id="donut-arc" cx="24" cy="24" r="20" fill="none" stroke="#16a34a" stroke-width="6"
              stroke-dasharray="125.66" stroke-dashoffset="125.66"
              stroke-linecap="round" transform="rotate(-90 24 24)"
              style="transition:stroke-dashoffset .6s ease,stroke .4s"/>
      <text id="donut-pct" x="24" y="28" text-anchor="middle" font-size="10" font-weight="700" fill="#e2e8f0">&#8212;</text>
    </svg>
    <button class="theme-btn" id="theme-toggle" title="Toggle dark mode" aria-label="Toggle theme">&#9790;</button>
  </div>
</header>

<div class="kpi-row" id="kpi-row">
  <div class="kpi"      ><div class="kpi-val" id="kv-total">&#8212;</div><div class="kpi-lbl">Total</div></div>
  <div class="kpi pass" ><div class="kpi-val" id="kv-pass" >&#8212;</div><div class="kpi-lbl">Pass</div></div>
  <div class="kpi fail" ><div class="kpi-val" id="kv-fail" >&#8212;</div><div class="kpi-lbl">Fail</div></div>
  <div class="kpi error"><div class="kpi-val" id="kv-error">&#8212;</div><div class="kpi-lbl">Error</div></div>
  <div class="kpi skip" ><div class="kpi-val" id="kv-skip" >&#8212;</div><div class="kpi-lbl">Skip</div></div>
  <div class="kpi"      ><div class="kpi-val" id="kv-rate" >&#8212;</div><div class="kpi-lbl">Pass Rate</div></div>
  <div class="kpi"      ><div class="kpi-val" id="kv-dur"  >&#8212;</div><div class="kpi-lbl">Total Duration</div></div>
  <div class="kpi"      ><div class="kpi-val" id="kv-avg"  >&#8212;</div><div class="kpi-lbl">Avg Duration</div></div>
</div>

<div class="pass-bar-wrap"><div class="pass-bar" id="pass-bar"></div></div>

<div class="tabs">
  <button class="tab-btn active" data-panel="panel-results"  >Results</button>
  <button class="tab-btn"        data-panel="panel-failures" >Failures</button>
  <button class="tab-btn"        data-panel="panel-ai"       >AI Analysis</button>
  <button class="tab-btn"        data-panel="panel-trends"   >Trends</button>
</div>

<div class="tab-panel active" id="panel-results">
  <div id="runsBar"><div class="runs-scroll" id="runsScroll"></div></div>
  <div class="filter-bar">
    <div class="pill-group" id="pills-results">
      <button class="pill active" data-st="all"  >All</button>
      <button class="pill p-pass" data-st="Pass" >Pass</button>
      <button class="pill p-fail" data-st="Fail" >Fail</button>
      <button class="pill p-error"data-st="Error">Error</button>
      <button class="pill p-skip" data-st="Skip" >Skip</button>
    </div>
    <select class="f-select" id="f-suite"   ><option value="">All Suites</option></select>
    <select class="f-select" id="f-cat"     ><option value="">All Categories</option></select>
    <select class="f-select" id="f-browser" ><option value="">All Browsers</option></select>
    <select class="f-select" id="f-env"     ><option value="">All Envs</option></select>
    <select class="f-select" id="f-ai-class" style="display:none"><option value="">All AI Classes</option></select>
    <select class="f-select" id="f-run"     style="display:none"><option value="">All Runs</option></select>
    <input  class="f-search" id="f-search"  placeholder="Search&#8230; (press /)"/>
    <select class="f-select" id="f-pagesize" style="width:110px"></select>
    <button class="f-clear-btn" id="f-clear">Clear</button>
  </div>
  <div class="tbl-toolbar">
    <div class="view-toggle">
      <button class="view-btn active" id="vbtn-table" onclick="setView('table')">&#8862; Table</button>
      <button class="view-btn"        id="vbtn-cards" onclick="setView('cards')">&#9783; Cards</button>
      <button class="view-btn"        id="vbtn-tree"  onclick="setView('tree')" >&#8863; Tree</button>
    </div>
    <button class="toolbar-btn" id="btnCsv" >Export CSV</button>
    <button class="toolbar-btn" id="btnJson">Export JSON</button>
  </div>
  <div id="view-table">
    <div class="tbl-wrap">
      <table class="results-table" id="results-table">
        <thead><tr>
          <th data-col="testName"  >Test Name</th>
          <th data-col="testSuite" >Class / Suite</th>
          <th data-col="status"    >Status</th>
          <th data-col="durationMs">Duration</th>
          <th data-col="browser"   >Browser / Exc</th>
          <th data-col="flakinessScore">Flakiness</th>
          <th style="cursor:default">Details</th>
        </tr></thead>
        <tbody id="results-tbody"></tbody>
      </table>
    </div>
    <div class="pagination" id="pagination"></div>
  </div>
  <div id="view-cards" style="display:none">
    <div class="cards-grid" id="cards-grid"></div>
    <div class="pagination" id="pagination-cards"></div>
  </div>
  <div id="view-tree" style="display:none">
    <div id="tree-view"></div>
  </div>
  <div id="load-msg">Loading&#8230;</div>
</div>

<div class="tab-panel" id="panel-failures">
  <div class="tbl-wrap">
    <table class="results-table" id="failures-table">
      <thead><tr>
        <th>Test Name</th>
        <th>Class / Suite</th>
        <th>Status</th>
        <th>Duration</th>
        <th>Exception</th>
        <th>Flakiness</th>
        <th style="cursor:default">Details</th>
      </tr></thead>
      <tbody id="failures-tbody"></tbody>
    </table>
  </div>
  <div class="pagination" id="pagination-fail"></div>
</div>

<div class="tab-panel" id="panel-ai">
  <div class="tbl-wrap">
    <table class="ai-table" id="ai-analysis-table">
      <thead><tr>
        <th>Test</th>
        <th>Class</th>
        <th>Status</th>
        <th>Classification</th>
        <th>Confidence</th>
        <th>Analysis</th>
        <th style="cursor:default">Details</th>
      </tr></thead>
      <tbody id="ai-tbody"></tbody>
    </table>
  </div>
  <div id="ai-empty-state" class="empty-state" style="display:none">No AI analysis data available.</div>
</div>

<div class="tab-panel" id="panel-trends">
  <div class="tbl-wrap">
    <table class="trends-table" id="trends-table">
      <thead><tr>
        <th>Run Name</th>
        <th>Total</th>
        <th>Pass</th>
        <th>Fail</th>
        <th>Error</th>
        <th>Skip</th>
        <th>Pass Rate</th>
      </tr></thead>
      <tbody id="trends-tbody"></tbody>
    </table>
  </div>
  <div id="trends-empty-state" class="empty-state" style="display:none">No run grouping data. Set RunName on TestResult to enable trends.</div>
</div>

</div>

<div class="modal-overlay" id="detail-modal">
  <div class="modal-box">
    <div class="modal-hdr">
      <h2 class="modal-title" id="modal-title">Test Detail</h2>
      <button class="modal-close" id="modal-close" aria-label="Close">&times;</button>
    </div>
    <div class="modal-tabs" id="modal-tabs">
      <button class="modal-tab-btn active" data-mpane="mpane-overview"  >Overview</button>
      <button class="modal-tab-btn"        data-mpane="mpane-failure"   >Failure</button>
      <button class="modal-tab-btn"        data-mpane="mpane-ai"        >AI Analysis</button>
      <button class="modal-tab-btn"        data-mpane="mpane-artifacts" >Artifacts</button>
    </div>
    <div class="modal-body">
      <div class="modal-pane active" id="mpane-overview">
        <table class="info-table"><tbody id="ov-body"></tbody></table>
      </div>
      <div class="modal-pane" id="mpane-failure">
        <div id="fail-none" style="color:var(--muted);font-style:italic;font-size:12px">No failure details.</div>
        <div id="fail-content" style="display:none">
          <div class="fail-exc-box" id="fail-exc"></div>
          <div class="assert-box" id="assert-box" style="display:none"></div>
          <div style="font-weight:700;font-size:12px;margin-bottom:4px">Stack Trace</div>
          <div class="stack-wrap">
            <pre class="stack-pre" id="fail-stack"></pre>
            <button class="copy-stack-btn" id="btn-copy-stack">Copy</button>
          </div>
          <button class="stack-toggle" id="stack-toggle">Show full stack</button>
        </div>
        <div id="skip-content" style="display:none">
          <div class="skip-box" id="skip-reason-box"></div>
        </div>
      </div>
      <div class="modal-pane" id="mpane-ai">
        <div id="ai-none" style="color:var(--muted);font-style:italic;font-size:12px">No AI analysis.</div>
        <div id="ai-content" style="display:none">
          <div style="margin-bottom:12px" id="ai-class-wrap"></div>
          <div class="ai-analysis-panel" id="ai-panel"></div>
        </div>
      </div>
      <div class="modal-pane" id="mpane-artifacts">
        <div id="art-screenshot" style="margin-bottom:16px;display:none">
          <div style="font-weight:700;font-size:12px;margin-bottom:8px">Screenshot</div>
          <img class="ss-img" id="art-ss-img" src="" alt="Screenshot" onclick="openLightbox(this.src)"/>
          <a class="open-link" id="art-ss-link" href="#" target="_blank">Open full size</a>
        </div>
        <div id="art-video" style="margin-bottom:16px;display:none">
          <div style="font-weight:700;font-size:12px;margin-bottom:8px">Screencast</div>
          <video class="modal-video" id="art-video-el" controls></video>
          <a class="open-link" id="art-video-link" href="#" target="_blank" style="display:none">Open Screencast</a>
        </div>
        <div id="art-network" style="margin-bottom:16px;display:none">
          <div style="font-weight:700;font-size:12px;margin-bottom:8px">Network</div>
          <a class="art-btn art-btn-primary" id="art-har" href="#" target="_blank">Download HAR</a>
          <a class="art-btn art-btn-success" id="art-excel" href="#" target="_blank">Download Excel</a>
        </div>
        <div id="art-diag" style="margin-bottom:16px;display:none">
          <div style="font-weight:700;font-size:12px;margin-bottom:8px">Diagnostics</div>
          <div style="font-size:11px;color:var(--muted);margin-bottom:6px" id="art-diag-path"></div>
          <a class="open-link" id="art-diag-link" href="#" target="_blank">Open Diagnostics Folder</a>
        </div>
        <div id="art-custom" style="display:none">
          <div style="font-weight:700;font-size:12px;margin-bottom:8px">Custom Properties</div>
          <table class="cp-table">
            <thead><tr><th>Key</th><th>Value</th></tr></thead>
            <tbody id="art-cp-body"></tbody>
          </table>
        </div>
        <div id="art-none" style="color:var(--muted);font-style:italic;font-size:12px">No artifacts attached.</div>
      </div>
    </div>
  </div>
</div>

<div id="lightbox" onclick="closeLightbox()">
  <button id="lightbox-close" onclick="closeLightbox()">&times;</button>
  <img id="lightbox-img" src="" alt=""/>
</div>

<script>
(function(){
'use strict';
[[JAVASCRIPT]]
})();
</script>
</body>
</html>
""";

        // ── JavaScript body (no script tags, no IIFE wrapper) ──────────────

        private static string GetJavaScript() => """
// Artifact link mode
const REPORT_BASE_URL = '[[REPORT_BASE_URL]]';

// Run data
const RUNS_DATA = [[RUNS_JSON]];
const HAS_RUNS  = RUNS_DATA.length >= 1;

// State
let ALL_ROWS    = [];
let FILTERED    = [];
let FAIL_ROWS   = [];
let activeStatus  = 'all';
let activeSuite   = '';
let activeCat     = '';
let activeBrowser = '';
let activeEnv     = '';
let activeRun     = '';
let activeAiClass = '';
let searchTerm    = '';
let pageSize      = 50;
let currentPage   = 1;
let failPage      = 1;
let currentView   = 'table';
let sortCol       = '';
let sortDir       = 1;
const PAGE_SIZES  = [25, 50, 100, 250, 500];

// ── Load data ──────────────────────────────────────────────────────────
[[DATA_INIT_SCRIPT]]

// ── Theme ──────────────────────────────────────────────────────────────
(function(){
  var btn = document.getElementById('theme-toggle');
  if(btn) btn.addEventListener('click', function(){
    var t = document.documentElement.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
    document.documentElement.setAttribute('data-theme', t);
    localStorage.setItem('sss-theme', t);
    btn.textContent = t === 'dark' ? '☀' : '☾';
  });
  var cur = document.documentElement.getAttribute('data-theme');
  if(btn) btn.textContent = cur === 'dark' ? '☀' : '☾';
})();

// ── Dropdowns ──────────────────────────────────────────────────────────
function populateDropdowns(){
  fillSelect('f-suite',   getUniq(ALL_ROWS, function(r){ return r.testSuite; }).sort());
  fillSelect('f-cat',     getUniq(ALL_ROWS, function(r){ return r.category; }).sort());
  fillSelect('f-browser', getUniq(ALL_ROWS, function(r){ return r.browser; }).sort());
  fillSelect('f-env',     getUniq(ALL_ROWS, function(r){ return r.environment; }).sort());
  var aiClasses = getUniq(ALL_ROWS, function(r){ return r.aiClassification; }).sort();
  if(aiClasses.length){
    fillSelect('f-ai-class', aiClasses);
    document.getElementById('f-ai-class').style.display = '';
  }
  if(HAS_RUNS){
    var runSel = document.getElementById('f-run');
    RUNS_DATA.forEach(function(rd){
      var o = document.createElement('option');
      o.value = rd.runName; o.textContent = rd.runName;
      runSel.appendChild(o);
    });
    runSel.style.display = '';
    document.getElementById('runsBar').style.display = '';
    renderRunsBar();
  }
  var ps = document.getElementById('f-pagesize');
  PAGE_SIZES.forEach(function(v){
    var o = document.createElement('option');
    o.value = v; o.textContent = v + ' / page';
    if(v === pageSize) o.selected = true;
    ps.appendChild(o);
  });
  renderTrends();
  applyFilters();
}
function getUniq(rows, fn){ return Array.from(new Set(rows.map(fn).filter(Boolean))); }
function fillSelect(id, vals){
  var s = document.getElementById(id);
  vals.forEach(function(v){
    var o = document.createElement('option'); o.value = v; o.textContent = v; s.appendChild(o);
  });
}

// ── Filter ─────────────────────────────────────────────────────────────
function applyFilters(){
  var q = searchTerm.toLowerCase();
  FILTERED = ALL_ROWS.filter(function(r){
    if(activeStatus !== 'all' && r.status !== activeStatus) return false;
    if(activeSuite   && r.testSuite   !== activeSuite)   return false;
    if(activeCat     && r.category    !== activeCat)     return false;
    if(activeBrowser && r.browser     !== activeBrowser) return false;
    if(activeEnv     && r.environment !== activeEnv)     return false;
    if(activeRun     && r.runName     !== activeRun)     return false;
    if(activeAiClass && r.aiClassification !== activeAiClass) return false;
    if(q && ![r.testName, r.testSuite, r.fullName, r.category, r.tags, r.browser, r.runName, r.exceptionMsg]
              .some(function(v){ return v && v.toLowerCase().indexOf(q) !== -1; })) return false;
    return true;
  });
  if(sortCol){
    FILTERED.sort(function(a, b){
      var av = a[sortCol] || '', bv = b[sortCol] || '';
      if(sortCol === 'durationMs' || sortCol === 'flakinessScore'){ av = +av; bv = +bv; }
      return av < bv ? -sortDir : av > bv ? sortDir : 0;
    });
  }
  FAIL_ROWS = FILTERED.filter(function(r){ return r.status === 'Fail' || r.status === 'Error'; });
  currentPage = 1; failPage = 1;
  renderStats(ALL_ROWS);
  if(currentView === 'table') renderTable();
  else if(currentView === 'cards') renderCards();
  else if(currentView === 'tree') buildTree(FILTERED);
  renderFailures();
  renderAiTab();
}

// ── Stats ──────────────────────────────────────────────────────────────
function renderStats(rows){
  var pass  = rows.filter(function(r){ return r.status==='Pass';  }).length;
  var fail  = rows.filter(function(r){ return r.status==='Fail';  }).length;
  var err   = rows.filter(function(r){ return r.status==='Error'; }).length;
  var skip  = rows.filter(function(r){ return r.status==='Skip';  }).length;
  var total = rows.length;
  var dur   = rows.reduce(function(s, r){ return s + r.durationMs; }, 0);
  var rate  = total ? Math.round(pass / total * 100) : 0;
  setText('kv-total', total);
  setText('kv-pass',  pass);
  setText('kv-fail',  fail);
  setText('kv-error', err);
  setText('kv-skip',  skip);
  setText('kv-rate',  total ? rate + '%' : 'N/A');
  setText('kv-dur',   fmtDur(dur));
  setText('kv-avg',   total ? fmtDur(dur / total) : 'N/A');
  setText('hdr-count', total);
  var arc = document.getElementById('donut-arc');
  var pct = document.getElementById('donut-pct');
  if(arc && pct){
    var circ = 125.66;
    arc.setAttribute('stroke-dashoffset', (circ - circ * rate / 100).toFixed(2));
    arc.setAttribute('stroke', rate===100 ? '#22c55e' : rate>=80 ? '#f59e0b' : '#ef4444');
    pct.textContent = total ? rate + '%' : '--';
  }
  var pb = document.getElementById('pass-bar');
  if(pb){
    pb.style.width = (total ? rate : 0) + '%';
    pb.style.background = rate===100 ? 'var(--color-pass)' : rate>=80 ? '#f59e0b' : 'var(--color-fail)';
  }
}
function setText(id, v){ var el = document.getElementById(id); if(el) el.textContent = v; }

// ── Helpers ─────────────────────────────────────────────────────────────
function fmtDur(ms){
  if(ms < 1000) return ms.toFixed(0) + 'ms';
  if(ms < 60000) return (ms / 1000).toFixed(1) + 's';
  return (ms / 60000).toFixed(1) + 'min';
}
function htmlEsc(s){
  return String(s || '').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;');
}
function toFileUrl(p){
  if(!p) return '';
  if(/^(https?|file):\/\//i.test(p)) return p;
  if(REPORT_BASE_URL) return REPORT_BASE_URL.replace(/\/+$/,'') + '/' + p.replace(/\\/g,'/');
  return 'file:///' + p.replace(/\\/g,'/');
}
function statusBadge(s){
  var c = {Pass:'pass',Fail:'fail',Error:'error',Skip:'skip'}[s] || 'skip';
  return '<span class="badge badge-' + c + '">' + htmlEsc(s) + '</span>';
}
function aiBadge(cls, conf, small){
  if(!cls) return '';
  var labels = {ProductIssue:'Product Issue',TestIssue:'Test Issue',Flaky:'Flaky',Infrastructure:'Infrastructure',Uncertain:'Uncertain'};
  var dot = {High:'●',Medium:'◑',Low:'○'}[conf] || '';
  var sz = small ? ' badge-ai-sm' : '';
  return '<span class="badge-ai badge-ai-' + htmlEsc(cls.toLowerCase()) + sz + '">'
    + htmlEsc(labels[cls] || cls)
    + (dot ? ' <span class="conf-dot" title="' + htmlEsc(conf) + ' confidence">' + dot + '</span>' : '')
    + '</span>';
}
var VIDEO_EXTS = /\.(mp4|webm|ogv|ogg|mov)(\?.*)?$/i;
function isVideo(p){ return VIDEO_EXTS.test(p); }

// ── Table rendering ─────────────────────────────────────────────────────
function renderTable(){
  var tbody = document.getElementById('results-tbody');
  if(!tbody) return;
  var start = (currentPage - 1) * pageSize;
  var slice = FILTERED.slice(start, start + pageSize);
  tbody.innerHTML = slice.map(function(r){
    var flak = r.flakinessScore ? (r.flakinessScore * 100).toFixed(0) + '%' : '';
    var info = r.browser || (r.exceptionType ? r.exceptionType : '');
    return '<tr class="row-' + r.status + '" data-idx="' + r.__index + '">'
      + '<td><div class="cell-name">' + htmlEsc(r.testName) + '</div>'
      + (r.runName ? '<div style="margin-top:2px"><span class="run-badge">' + htmlEsc(r.runName) + '</span></div>' : '')
      + '</td>'
      + '<td><div class="cell-suite">' + htmlEsc(r.testSuite || '') + '</div></td>'
      + '<td>' + statusBadge(r.status) + '</td>'
      + '<td class="cell-dur">' + fmtDur(r.durationMs) + '</td>'
      + '<td class="cell-info" title="' + htmlEsc(info) + '">' + htmlEsc(info) + '</td>'
      + '<td class="cell-flak">' + flak + '</td>'
      + '<td><button class="details-btn" data-idx="' + r.__index + '">Details</button></td>'
      + '</tr>';
  }).join('');
  tbody.querySelectorAll('.cell-name, .details-btn').forEach(function(el){
    el.addEventListener('click', function(){ openDetail(parseInt(el.dataset.idx)); });
  });
  renderPagination('pagination', FILTERED.length, currentPage, function(p){ currentPage = p; renderTable(); });
}

// ── Cards rendering ─────────────────────────────────────────────────────
function renderCards(){
  var grid = document.getElementById('cards-grid');
  if(!grid) return;
  var start = (currentPage - 1) * pageSize;
  var slice = FILTERED.slice(start, start + pageSize);
  grid.innerHTML = slice.map(function(r){
    return '<div class="result-card st-' + r.status + '" data-idx="' + r.__index + '">'
      + '<div class="card-name">' + htmlEsc(r.testName) + '</div>'
      + '<div class="card-suite">' + htmlEsc(r.testSuite || '(no suite)') + '</div>'
      + '<div class="card-meta">' + statusBadge(r.status)
      + '<span style="font-size:11px;color:var(--muted)">' + fmtDur(r.durationMs) + '</span>'
      + (r.browser ? '<span style="font-size:11px;color:var(--muted)">' + htmlEsc(r.browser) + '</span>' : '')
      + '</div>'
      + (aiBadge(r.aiClassification,'',true) ? '<div style="margin-top:6px">' + aiBadge(r.aiClassification,r.aiConfidence,true) + '</div>' : '')
      + '</div>';
  }).join('');
  grid.querySelectorAll('.result-card').forEach(function(el){
    el.addEventListener('click', function(){ openDetail(parseInt(el.dataset.idx)); });
  });
  renderPagination('pagination-cards', FILTERED.length, currentPage, function(p){ currentPage = p; renderCards(); });
}

// ── Failures rendering ─────────────────────────────────────────────────
function renderFailures(){
  var tbody = document.getElementById('failures-tbody');
  if(!tbody) return;
  var start = (failPage - 1) * pageSize;
  var slice = FAIL_ROWS.slice(start, start + pageSize);
  tbody.innerHTML = slice.map(function(r){
    var exc = r.exceptionType || r.exceptionMsg || '';
    return '<tr class="row-' + r.status + '" data-idx="' + r.__index + '">'
      + '<td><div class="cell-name">' + htmlEsc(r.testName) + '</div></td>'
      + '<td><div class="cell-suite">' + htmlEsc(r.testSuite || '') + '</div></td>'
      + '<td>' + statusBadge(r.status) + '</td>'
      + '<td class="cell-dur">' + fmtDur(r.durationMs) + '</td>'
      + '<td class="cell-info" title="' + htmlEsc(exc) + '">' + htmlEsc(exc) + '</td>'
      + '<td class="cell-flak">' + (r.flakinessScore ? (r.flakinessScore*100).toFixed(0)+'%' : '') + '</td>'
      + '<td><button class="details-btn" data-idx="' + r.__index + '">Details</button></td>'
      + '</tr>';
  }).join('');
  tbody.querySelectorAll('.cell-name, .details-btn').forEach(function(el){
    el.addEventListener('click', function(){ openDetail(parseInt(el.dataset.idx)); });
  });
  renderPagination('pagination-fail', FAIL_ROWS.length, failPage, function(p){ failPage = p; renderFailures(); });
}

// ── AI tab rendering ────────────────────────────────────────────────────
function renderAiTab(){
  var tbody = document.getElementById('ai-tbody');
  var emptyState = document.getElementById('ai-empty-state');
  if(!tbody) return;
  var aiRows = ALL_ROWS.filter(function(r){ return r.aiClassification; });
  if(!aiRows.length){
    tbody.innerHTML = '';
    if(emptyState) emptyState.style.display = '';
    return;
  }
  if(emptyState) emptyState.style.display = 'none';
  tbody.innerHTML = aiRows.map(function(r){
    var raw = r.aiAnalysis || '';
    var snippet = raw.replace(/\s+/g,' ').substring(0,80) + (raw.length > 80 ? '…' : '');
    return '<tr class="row-' + r.status + '" data-idx="' + r.__index + '">'
      + '<td><div class="cell-name">' + htmlEsc(r.testName) + '</div></td>'
      + '<td><div class="cell-suite">' + htmlEsc(r.testSuite || '') + '</div></td>'
      + '<td>' + statusBadge(r.status) + '</td>'
      + '<td>' + aiBadge(r.aiClassification, r.aiConfidence, false) + '</td>'
      + '<td style="font-size:11px">' + htmlEsc(r.aiConfidence || '') + '</td>'
      + '<td><div class="ai-snippet">' + htmlEsc(snippet) + '</div></td>'
      + '<td><button class="details-btn" data-idx="' + r.__index + '">Details</button></td>'
      + '</tr>';
  }).join('');
  tbody.querySelectorAll('.cell-name, .details-btn').forEach(function(el){
    el.addEventListener('click', function(){ openDetail(parseInt(el.dataset.idx), 'mpane-ai'); });
  });
}

// ── Trends rendering ────────────────────────────────────────────────────
function renderTrends(){
  var tbody = document.getElementById('trends-tbody');
  var empty = document.getElementById('trends-empty-state');
  if(!tbody) return;
  if(!HAS_RUNS || !RUNS_DATA.length){
    tbody.innerHTML = '';
    if(empty) empty.style.display = '';
    return;
  }
  if(empty) empty.style.display = 'none';
  tbody.innerHTML = RUNS_DATA.map(function(rd){
    var rate = rd.total ? Math.round(rd.pass / rd.total * 100) : 0;
    var barCol = rate===100 ? 'var(--color-pass)' : rate>=80 ? '#f59e0b' : 'var(--color-fail)';
    return '<tr>'
      + '<td style="font-weight:600">' + htmlEsc(rd.runName) + '</td>'
      + '<td>' + rd.total + '</td>'
      + '<td style="color:var(--color-pass)">' + rd.pass + '</td>'
      + '<td style="color:var(--color-fail)">' + (rd.fail || 0) + '</td>'
      + '<td style="color:var(--color-error)">' + (rd.error || 0) + '</td>'
      + '<td style="color:var(--color-skip)">' + (rd.skip || 0) + '</td>'
      + '<td><div class="trend-bar-wrap"><div class="trend-bar-bg"><div class="trend-bar-fill" style="width:' + rate + '%;background:' + barCol + '"></div></div>'
      + '<span style="font-size:11px;white-space:nowrap">' + rate + '%</span></div></td>'
      + '</tr>';
  }).join('');
}

// ── Pagination ─────────────────────────────────────────────────────────
function renderPagination(containerId, total, page, onPage){
  var el = document.getElementById(containerId);
  if(!el) return;
  var pages = Math.ceil(total / pageSize);
  if(pages <= 1){ el.innerHTML = ''; return; }
  var html = '';
  html += '<button class="pg-btn"' + (page<=1?' disabled':'') + ' data-p="' + (page-1) + '">&lsaquo;</button>';
  pageNums(page, pages).forEach(function(n){
    if(n === '...') html += '<span class="pg-ellipsis">&#8230;</span>';
    else html += '<button class="pg-btn' + (n===page?' active':'') + '" data-p="' + n + '">' + n + '</button>';
  });
  html += '<button class="pg-btn"' + (page>=pages?' disabled':'') + ' data-p="' + (page+1) + '">&rsaquo;</button>';
  html += '<span class="pg-info">' + total + ' results</span>';
  el.innerHTML = html;
  el.querySelectorAll('.pg-btn:not([disabled])').forEach(function(btn){
    btn.addEventListener('click', function(){ onPage(parseInt(btn.dataset.p)); });
  });
}
function pageNums(cur, total){
  if(total <= 7){ var r=[]; for(var i=1;i<=total;i++) r.push(i); return r; }
  var result = [1];
  if(cur > 3) result.push('...');
  for(var i=Math.max(2,cur-2); i<=Math.min(total-1,cur+2); i++) result.push(i);
  if(cur < total-2) result.push('...');
  result.push(total);
  return result;
}

// ── Runs bar ───────────────────────────────────────────────────────────
function renderRunsBar(){
  var scroll = document.getElementById('runsScroll');
  if(!scroll) return;
  scroll.innerHTML = RUNS_DATA.map(function(rd){
    var pct = rd.total ? Math.round(rd.pass / rd.total * 100) : 0;
    var barCol = pct===100 ? '#22c55e' : pct>=80 ? '#f59e0b' : '#ef4444';
    var isActive = activeRun === rd.runName;
    return '<div class="run-card' + (isActive ? ' active' : '') + '" onclick="toggleRunFilter(' + JSON.stringify(rd.runName) + ')">'
      + '<div class="run-name" title="' + htmlEsc(rd.runName) + '">' + htmlEsc(rd.runName) + '</div>'
      + '<div class="run-stats">'
      + '<span style="color:var(--color-pass)">' + rd.pass + '✓</span>'
      + (rd.fail  ? '<span style="color:var(--color-fail)">'  + rd.fail  + '✗</span>' : '')
      + (rd.error ? '<span style="color:var(--color-error)">' + rd.error + '!</span>' : '')
      + (rd.skip  ? '<span style="color:var(--color-skip)">'  + rd.skip  + '⊘</span>' : '')
      + '</div>'
      + '<div class="run-progress-bar"><div class="run-progress-fill" style="width:' + pct + '%;background:' + barCol + '"></div></div>'
      + '<div class="run-pass-rate">' + pct + '% pass &middot; ' + rd.total + ' test' + (rd.total!==1?'s':'') + '</div>'
      + '</div>';
  }).join('');
}
window.toggleRunFilter = function(run){
  activeRun = (activeRun === run) ? '' : run;
  document.getElementById('f-run').value = activeRun;
  renderRunsBar();
  applyFilters();
};

// ── Detail modal ────────────────────────────────────────────────────────
var currentStack = '', stackCollapsed = true;

window.openDetail = function(idx, initialPane){
  var r = ALL_ROWS[idx];
  if(!r) return;
  var title = r.testName + (r.testSuite ? ' · ' + r.testSuite : '');
  document.getElementById('modal-title').textContent = title;
  switchModalPane(initialPane || 'mpane-overview');

  // Overview
  var fields = [
    ['Test Name',   r.testName],
    ['Full Name',   r.fullName],
    ['Suite',       r.testSuite],
    ['Category',    r.category],
    ['Tags',        r.tags],
    ['Status',      statusBadge(r.status)],
    ['Duration',    fmtDur(r.durationMs)],
    ['Start Time',  r.startTime],
    ['Run',         r.runName],
    ['Browser',     r.browser],
    ['Environment', r.environment],
    ['Machine',     r.machineName],
  ].filter(function(f){ return f[1]; });
  document.getElementById('ov-body').innerHTML = fields.map(function(f){
    return '<tr><th>' + htmlEsc(f[0]) + '</th><td>' + f[1] + '</td></tr>';
  }).join('');

  // Failure
  var hasF = r.exceptionType || r.exceptionMsg || r.stackTrace;
  var hasS = r.skipReason;
  document.getElementById('fail-none').style.display    = (!hasF && !hasS) ? '' : 'none';
  document.getElementById('fail-content').style.display = hasF ? '' : 'none';
  document.getElementById('skip-content').style.display = (!hasF && hasS) ? '' : 'none';
  if(hasF){
    document.getElementById('fail-exc').textContent =
      (r.exceptionType ? r.exceptionType + ': ' : '') + (r.exceptionMsg || '');
    var ab = document.getElementById('assert-box');
    if(r.assertMsg){ ab.style.display = ''; ab.textContent = r.assertMsg; } else { ab.style.display = 'none'; }
    currentStack = r.stackTrace || '';
    stackCollapsed = true;
    renderStack();
  }
  if(hasS) document.getElementById('skip-reason-box').textContent = r.skipReason || '';

  // AI
  var hasAi = r.aiAnalysis || r.aiClassification;
  document.getElementById('ai-none').style.display    = hasAi ? 'none' : '';
  document.getElementById('ai-content').style.display = hasAi ? '' : 'none';
  if(hasAi){
    document.getElementById('ai-class-wrap').innerHTML = aiBadge(r.aiClassification, r.aiConfidence, false);
    var pan = document.getElementById('ai-panel');
    pan.style.display = r.aiAnalysis ? '' : 'none';
    if(r.aiAnalysis) pan.innerHTML = renderAiText(r.aiAnalysis);
  }

  // Artifacts
  var hasSs  = !!r.screenshot, hasVid = !!r.screencast;
  var hasNet = r.networkHar || r.networkExcel, hasDiag = !!r.diagnostics;
  var hasCp  = r.customProps && Object.keys(r.customProps).length > 0;
  var anyArt = hasSs || hasVid || hasNet || hasDiag || hasCp;
  document.getElementById('art-none').style.display = anyArt ? 'none' : '';
  // Screenshot
  document.getElementById('art-screenshot').style.display = hasSs ? '' : 'none';
  if(hasSs){
    document.getElementById('art-ss-img').src  = toFileUrl(r.screenshot);
    document.getElementById('art-ss-link').href = toFileUrl(r.screenshot);
  }
  // Video
  document.getElementById('art-video').style.display = hasVid ? '' : 'none';
  if(hasVid){
    var vel  = document.getElementById('art-video-el');
    var vlnk = document.getElementById('art-video-link');
    if(isVideo(r.screencast)){
      vel.style.display = ''; vel.src = toFileUrl(r.screencast);
      vlnk.style.display = 'none';
    } else {
      vel.style.display = 'none';
      vlnk.style.display = ''; vlnk.href = toFileUrl(r.screencast);
    }
  }
  // Network
  document.getElementById('art-network').style.display = hasNet ? '' : 'none';
  if(hasNet){
    var harEl = document.getElementById('art-har');
    var xlsEl = document.getElementById('art-excel');
    harEl.style.display = r.networkHar   ? '' : 'none';
    xlsEl.style.display = r.networkExcel ? '' : 'none';
    if(r.networkHar)   harEl.href = toFileUrl(r.networkHar);
    if(r.networkExcel) xlsEl.href = toFileUrl(r.networkExcel);
  }
  // Diagnostics
  document.getElementById('art-diag').style.display = hasDiag ? '' : 'none';
  if(hasDiag){
    document.getElementById('art-diag-path').textContent = r.diagnostics;
    document.getElementById('art-diag-link').href = toFileUrl(r.diagnostics);
  }
  // Custom props
  document.getElementById('art-custom').style.display = hasCp ? '' : 'none';
  if(hasCp){
    document.getElementById('art-cp-body').innerHTML =
      Object.entries(r.customProps).map(function(kv){
        return '<tr><td>' + htmlEsc(kv[0]) + '</td><td>' + htmlEsc(String(kv[1])) + '</td></tr>';
      }).join('');
  }

  document.getElementById('detail-modal').classList.add('open');
  document.body.style.overflow = 'hidden';
};

// ── Modal tab switching ────────────────────────────────────────────────
function switchModalPane(paneId){
  document.querySelectorAll('.modal-tab-btn').forEach(function(b){
    b.classList.toggle('active', b.dataset.mpane === paneId);
  });
  document.querySelectorAll('.modal-pane').forEach(function(p){
    p.classList.toggle('active', p.id === paneId);
  });
}
document.querySelectorAll('.modal-tab-btn').forEach(function(b){
  b.addEventListener('click', function(){ switchModalPane(b.dataset.mpane); });
});
document.getElementById('modal-close').addEventListener('click', function(){ closeModal(); });
document.getElementById('detail-modal').addEventListener('click', function(e){
  if(e.target === this) closeModal();
});
function closeModal(){
  document.getElementById('detail-modal').classList.remove('open');
  document.body.style.overflow = '';
  var vel = document.getElementById('art-video-el');
  if(vel){ vel.pause(); vel.src = ''; }
}

// ── AI text renderer ───────────────────────────────────────────────────
function renderAiText(text){
  return text
    .replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;')
    .replace(/\*\*(.+?)\*\*/g,'<strong>$1</strong>')
    .replace(/^XPATH_CANDIDATE:\s*(.+)$/gm, function(_,x){ return '<span class="xpath-candidate">XPATH_CANDIDATE: '+x+'</span>'; })
    .replace(/\n/g,'<br>');
}

// ── Stack trace ────────────────────────────────────────────────────────
function renderStack(){
  var MAX = 600;
  var pre = document.getElementById('fail-stack');
  var tog = document.getElementById('stack-toggle');
  if(!currentStack){ pre.textContent = '(no stack trace)'; tog.style.display = 'none'; return; }
  if(currentStack.length <= MAX){ pre.textContent = currentStack; tog.style.display = 'none'; return; }
  pre.textContent = stackCollapsed ? currentStack.substring(0, MAX) + '…' : currentStack;
  tog.textContent = stackCollapsed ? 'Show full stack' : 'Collapse stack';
  tog.style.display = '';
}
document.getElementById('stack-toggle').addEventListener('click', function(){
  stackCollapsed = !stackCollapsed; renderStack();
});
document.getElementById('btn-copy-stack').addEventListener('click', function(){
  var btn = this;
  navigator.clipboard.writeText(currentStack).then(function(){
    btn.textContent = 'Copied!';
    setTimeout(function(){ btn.textContent = 'Copy'; }, 1500);
  });
});

// ── Lightbox ──────────────────────────────────────────────────────────
window.openLightbox = function(src){
  document.getElementById('lightbox-img').src = src;
  document.getElementById('lightbox').classList.add('open');
};
window.closeLightbox = function(){
  document.getElementById('lightbox').classList.remove('open');
};

// ── View toggle ───────────────────────────────────────────────────────
window.setView = function(v){
  currentView = v;
  document.getElementById('view-table').style.display = v === 'table' ? '' : 'none';
  document.getElementById('view-cards').style.display = v === 'cards' ? '' : 'none';
  document.getElementById('view-tree').style.display  = v === 'tree'  ? '' : 'none';
  ['table','cards','tree'].forEach(function(n){
    var b = document.getElementById('vbtn-' + n);
    if(b) b.classList.toggle('active', n === v);
  });
  if(v === 'tree') buildTree(FILTERED);
  else if(v === 'table') renderTable();
  else renderCards();
};

// ── Table sort ────────────────────────────────────────────────────────
document.querySelectorAll('#results-table th[data-col]').forEach(function(th){
  th.addEventListener('click', function(){
    var col = th.dataset.col;
    if(sortCol === col){ sortDir *= -1; } else { sortCol = col; sortDir = 1; }
    document.querySelectorAll('#results-table th').forEach(function(h){
      h.classList.remove('sort-asc','sort-desc');
    });
    th.classList.add(sortDir === 1 ? 'sort-asc' : 'sort-desc');
    applyFilters();
  });
});

// ── Tab switching ─────────────────────────────────────────────────────
document.querySelectorAll('.tab-btn').forEach(function(btn){
  btn.addEventListener('click', function(){
    var panel = btn.dataset.panel;
    document.querySelectorAll('.tab-btn').forEach(function(b){ b.classList.remove('active'); });
    document.querySelectorAll('.tab-panel').forEach(function(p){ p.classList.remove('active'); });
    btn.classList.add('active');
    var el = document.getElementById(panel);
    if(el) el.classList.add('active');
  });
});

// ── Filter wiring ─────────────────────────────────────────────────────
document.querySelectorAll('#pills-results .pill').forEach(function(p){
  p.addEventListener('click', function(){
    document.querySelectorAll('#pills-results .pill').forEach(function(x){ x.classList.remove('active'); });
    p.classList.add('active');
    activeStatus = p.dataset.st;
    applyFilters();
  });
});
document.getElementById('f-suite'   ).addEventListener('change', function(e){ activeSuite   = e.target.value; applyFilters(); });
document.getElementById('f-cat'     ).addEventListener('change', function(e){ activeCat     = e.target.value; applyFilters(); });
document.getElementById('f-browser' ).addEventListener('change', function(e){ activeBrowser = e.target.value; applyFilters(); });
document.getElementById('f-env'     ).addEventListener('change', function(e){ activeEnv     = e.target.value; applyFilters(); });
document.getElementById('f-run'     ).addEventListener('change', function(e){ activeRun     = e.target.value; renderRunsBar(); applyFilters(); });
document.getElementById('f-ai-class').addEventListener('change', function(e){ activeAiClass = e.target.value; applyFilters(); });
document.getElementById('f-search'  ).addEventListener('input',  function(e){ searchTerm    = e.target.value; applyFilters(); });
document.getElementById('f-pagesize').addEventListener('change', function(e){ pageSize = parseInt(e.target.value); currentPage = 1; failPage = 1; applyFilters(); });
document.getElementById('f-clear').addEventListener('click', function(){
  activeStatus = 'all'; activeSuite = ''; activeCat = ''; activeBrowser = ''; activeEnv = ''; activeRun = ''; activeAiClass = ''; searchTerm = '';
  ['f-search','f-suite','f-cat','f-browser','f-env','f-run','f-ai-class'].forEach(function(id){
    var e = document.getElementById(id); if(e) e.value = '';
  });
  document.querySelectorAll('#pills-results .pill').forEach(function(p){ p.classList.toggle('active', p.dataset.st === 'all'); });
  renderRunsBar();
  applyFilters();
});

// ── Keyboard shortcuts ────────────────────────────────────────────────
document.addEventListener('keydown', function(e){
  var tag = document.activeElement.tagName;
  if(e.key === '/' && tag !== 'INPUT' && tag !== 'TEXTAREA'){
    e.preventDefault(); document.getElementById('f-search').focus();
  }
  if(e.key === 'Escape'){
    if(document.getElementById('detail-modal').classList.contains('open')){ closeModal(); return; }
    if(document.getElementById('lightbox').classList.contains('open')){ closeLightbox(); return; }
    document.getElementById('f-search').blur();
    document.getElementById('f-search').value = ''; searchTerm = ''; applyFilters();
  }
});

// ── Export ─────────────────────────────────────────────────────────────
document.getElementById('btnCsv' ).addEventListener('click', function(){ exportCsv();  });
document.getElementById('btnJson').addEventListener('click', function(){ exportJson(); });
function exportCsv(){
  var cols = ['runName','testName','testSuite','category','status','durationMs','browser','environment','machineName','tags','exceptionType','exceptionMsg'];
  var lines = FILTERED.map(function(r){ return cols.map(function(k){ return csvCell(r[k]); }).join(','); });
  dlFile('test-report.csv', cols.join(',') + '\n' + lines.join('\n'), 'text/csv');
}
function exportJson(){ dlFile('test-report.json', JSON.stringify(FILTERED, null, 2), 'application/json'); }
function csvCell(v){ var s = String(v == null ? '' : v).replace(/"/g,'""'); return /[",\n]/.test(s) ? '"'+s+'"' : s; }
function dlFile(name, data, type){
  var a = document.createElement('a');
  a.href = URL.createObjectURL(new Blob([data],{type:type}));
  a.download = name; a.click();
}

// ── Tree view ─────────────────────────────────────────────────────────
function buildTree(rows){
  var container = document.getElementById('tree-view');
  container.innerHTML = '';
  if(!rows.length){
    container.innerHTML = '<div class="empty-state">No results match current filters.</div>';
    return;
  }
  if(HAS_RUNS){
    var runMap = new Map();
    rows.forEach(function(r){ var k = r.runName||'(No Run)'; if(!runMap.has(k)) runMap.set(k,[]); runMap.get(k).push(r); });
    Array.from(runMap.entries()).sort(cmpGroupByFail).forEach(function(e){ container.appendChild(makeRunBlock(e[0],e[1])); });
  } else {
    var suiteMap = new Map();
    rows.forEach(function(r){ var k = r.testSuite||'(No Suite)'; if(!suiteMap.has(k)) suiteMap.set(k,[]); suiteMap.get(k).push(r); });
    Array.from(suiteMap.entries()).sort(cmpGroupByFail).forEach(function(e){ container.appendChild(makeSuiteBlock(e[0],e[1])); });
  }
}
function cmpGroupByFail(a, b){
  var af = a[1].some(function(t){ return t.status==='Fail'||t.status==='Error'; });
  var bf = b[1].some(function(t){ return t.status==='Fail'||t.status==='Error'; });
  return af===bf ? 0 : af ? -1 : 1;
}
function makeRunBlock(name, tests){
  var pass=tests.filter(function(t){return t.status==='Pass';}).length;
  var fail=tests.filter(function(t){return t.status==='Fail';}).length;
  var err=tests.filter(function(t){return t.status==='Error';}).length;
  var skip=tests.filter(function(t){return t.status==='Skip';}).length;
  var dur=tests.reduce(function(s,t){return s+t.durationMs;},0);
  var expanded=fail>0||err>0;
  var suiteMap=new Map();
  tests.forEach(function(r){var k=r.testSuite||'(No Suite)';if(!suiteMap.has(k))suiteMap.set(k,[]);suiteMap.get(k).push(r);});
  var suiteBlocks=Array.from(suiteMap.entries()).sort(cmpGroupByFail).map(function(e){return makeSuiteBlock(e[0],e[1]).outerHTML;}).join('');
  var el=document.createElement('div'); el.className='run-block';
  el.innerHTML='<div class="run-block-header" onclick="var b=this.nextElementSibling;b.classList.toggle(\'open\');this.querySelector(\'.run-block-chevron\').textContent=b.classList.contains(\'open\')?\'▼\':\'▶\'">'
    +'<span class="run-block-chevron">'+(expanded?'▼':'▶')+'</span>'
    +'<div class="suite-counts">'
    +(pass?'<span class="suite-count pass">'+pass+'</span>':'')
    +(fail?'<span class="suite-count fail">'+fail+'</span>':'')
    +(err?'<span class="suite-count error">'+err+'</span>':'')
    +(skip?'<span class="suite-count skip">'+skip+'</span>':'')
    +'</div>'
    +'<span class="run-block-name" title="'+htmlEsc(name)+'">'+htmlEsc(name)+'</span>'
    +'<span class="suite-meta" style="color:#94a3b8">'+fmtDur(dur)+'&nbsp;&middot;&nbsp;'+tests.length+'&nbsp;test'+(tests.length!==1?'s':'')+'</span>'
    +'</div>'
    +'<div class="run-block-body'+(expanded?' open':'')+'" style="padding:8px 8px 4px">'+suiteBlocks+'</div>';
  return el;
}
function makeSuiteBlock(name, tests){
  var pass=tests.filter(function(t){return t.status==='Pass';}).length;
  var fail=tests.filter(function(t){return t.status==='Fail';}).length;
  var err=tests.filter(function(t){return t.status==='Error';}).length;
  var skip=tests.filter(function(t){return t.status==='Skip';}).length;
  var dur=tests.reduce(function(s,t){return s+t.durationMs;},0);
  var expanded=fail>0||err>0;
  var el=document.createElement('div'); el.className='suite-block';
  el.innerHTML='<div class="suite-header" onclick="var sb=this.nextElementSibling;sb.classList.toggle(\'open\');this.querySelector(\'.suite-chevron\').textContent=sb.classList.contains(\'open\')?\'▼\':\'▶\'">'
    +'<span class="suite-chevron">'+(expanded?'▼':'▶')+'</span>'
    +'<div class="suite-counts">'
    +(pass?'<span class="suite-count pass">'+pass+'</span>':'')
    +(fail?'<span class="suite-count fail">'+fail+'</span>':'')
    +(err?'<span class="suite-count error">'+err+'</span>':'')
    +(skip?'<span class="suite-count skip">'+skip+'</span>':'')
    +'</div>'
    +'<span class="suite-name" title="'+htmlEsc(name)+'">'+htmlEsc(name)+'</span>'
    +'<span class="suite-meta">'+fmtDur(dur)+'&nbsp;&middot;&nbsp;'+tests.length+'&nbsp;test'+(tests.length!==1?'s':'')+'</span>'
    +'</div>'
    +'<div class="suite-body'+(expanded?' open':'')+'">'+tests.map(function(t){ return makeTestRow(t); }).join('')+'</div>';
  return el;
}
function makeTestRow(r){
  var icon = {Pass:'✓',Fail:'✕',Skip:'⊘',Error:'!'}[r.status] || '?';
  return '<div class="test-row" data-st="'+r.status+'" onclick="this.nextElementSibling.classList.toggle(\'open\')">'
    +'<span class="test-status-icon '+r.status+'">'+icon+'</span>'
    +'<span class="test-name-label">'+htmlEsc(r.testName)+'</span>'
    +'<span class="test-duration-label">'+fmtDur(r.durationMs)+'</span>'
    +'</div>'
    +'<div class="test-detail-panel">'+makeTestDetail(r)+'</div>';
}
function makeTestDetail(r){
  var sections = [];
  var ovFields = [['Status',statusBadge(r.status)],['Duration',fmtDur(r.durationMs)],['Suite',r.testSuite],['Category',r.category],['Tags',r.tags],['Browser',r.browser],['Environment',r.environment],['Machine',r.machineName]].filter(function(f){return f[1];});
  sections.push({id:'ov',label:'Overview',active:true,html:'<table style="font-size:12px;border-collapse:collapse">'+ovFields.map(function(f){return '<tr><td style="padding:2px 14px 2px 0;color:var(--muted);white-space:nowrap">'+htmlEsc(f[0])+'</td><td style="padding:2px 0">'+f[1]+'</td></tr>';}).join('')+'</table>'});
  if(r.exceptionMsg||r.stackTrace||r.assertMsg){
    sections.push({id:'fl',label:'Failure',active:false,html:'<div class="failure-box">'+htmlEsc((r.exceptionType?r.exceptionType+': ':'')+r.exceptionMsg)+'</div>'+(r.assertMsg?'<div style="background:#fffbeb;border-left:3px solid #fbbf24;padding:6px 12px;border-radius:0 4px 4px 0;font-size:12px;margin-bottom:8px">'+htmlEsc(r.assertMsg)+'</div>':'')+(r.stackTrace?'<pre class="inline-stack-box">'+htmlEsc(r.stackTrace)+'</pre>':'')});
  }
  if(r.screenshot){ var u=toFileUrl(r.screenshot); sections.push({id:'ss',label:'Screenshot',active:false,html:'<img src="'+htmlEsc(u)+'" style="max-width:100%;border-radius:4px;border:1px solid var(--border);cursor:zoom-in" onclick="openLightbox(\''+htmlEsc(u)+'\')" alt="screenshot"/><div style="margin-top:6px"><a href="'+htmlEsc(u)+'" target="_blank" class="open-link">Open full size</a></div>'}); }
  if(r.screencast){ var u2=toFileUrl(r.screencast); sections.push({id:'vid',label:'Video',active:false,html:isVideo(r.screencast)?'<video controls style="width:100%;max-height:280px;background:#000;border-radius:4px"><source src="'+htmlEsc(u2)+'"/></video>':'<a href="'+htmlEsc(u2)+'" target="_blank" class="open-link">Open Video</a>'}); }
  if(r.networkHar||r.networkExcel){ sections.push({id:'net',label:'Network',active:false,html:(r.networkHar?'<a href="'+htmlEsc(toFileUrl(r.networkHar))+'" target="_blank" class="art-btn art-btn-primary">Download HAR</a>':'')+(r.networkExcel?'<a href="'+htmlEsc(toFileUrl(r.networkExcel))+'" target="_blank" class="art-btn art-btn-success">Download Excel</a>':'')}); }
  if(r.aiAnalysis){ sections.push({id:'ai',label:'AI Analysis',active:false,html:'<div class="ai-analysis-panel">'+renderAiText(r.aiAnalysis)+'</div>'}); }
  var tabs  = sections.map(function(s){ return '<button class="detail-tab-btn'+(s.active?' active':'')+'" data-tdtab="'+s.id+'" onclick="switchTreeDetailTab(this)">'+s.label+'</button>'; }).join('');
  var panes = sections.map(function(s){ return '<div class="detail-tab-pane'+(s.active?' active':'')+'" data-tdpane="'+s.id+'">'+s.html+'</div>'; }).join('');
  return '<div class="detail-tabs-row">'+tabs+'</div>'+panes;
}
window.switchTreeDetailTab = function(btn){
  var panel = btn.closest('.test-detail-panel'), id = btn.dataset.tdtab;
  panel.querySelectorAll('.detail-tab-btn').forEach(function(b){ b.classList.toggle('active', b.dataset.tdtab === id); });
  panel.querySelectorAll('.detail-tab-pane').forEach(function(p){ p.classList.toggle('active', p.dataset.tdpane === id); });
};
""";
    }
}
