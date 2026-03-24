using SimpleSeleniumSupport.Analytics;
using SimpleSeleniumSupport.CiExport;
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
            => GetTemplate()
                .Replace("[[REPORT_NAME]]",      HtmlEnc(reportName ?? safeName))
                .Replace("[[GEN_TIME]]",          DateTime.UtcNow.ToString("u"))
                .Replace("[[RUNS_JSON]]",         runsJson)
                .Replace("[[REPORT_TYPE]]",       reportType)
                .Replace("[[DATA_INIT_SCRIPT]]",  dataInitScript);

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
            screenshot     = r.ScreenshotPath ?? "",
            screencast     = r.ScreencastPath ?? "",
            diagnostics    = r.DiagnosticsFolder ?? "",
            networkHar     = r.NetworkHarPath ?? "",
            networkExcel   = r.NetworkExcelPath ?? "",
            aiAnalysis       = r.AiAnalysis       ?? "",
            aiClassification = r.AiClassification ?? "",
            aiConfidence     = r.AiConfidence     ?? "",
            customProps      = r.CustomProperties
        };

        // ── Utilities ──────────────────────────────────────────────────────────

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
:root {
  --color-pass: #198754;
  --color-fail: #dc3545;
  --color-skip: #6c757d;
  --color-error: #fd7e14;
  --bg-page: #f5f6f8;
  --bg-card: #fff;
  --border-color: #e2e8f0;
}
body {
  background: var(--bg-page);
  font-size: 13px;
  margin: 0;
}
/* ── Top bar ── */
#topbar {
  background: #1e293b;
  color: #f1f5f9;
  padding: 9px 20px;
  display: flex;
  align-items: center;
  gap: 12px;
  position: sticky;
  top: 0;
  z-index: 200;
}
#topbar h1 {
  font-size: 15px;
  margin: 0;
  font-weight: 600;
}
/* ── Stats bar ── */
#statsBar {
  display: flex;
  gap: 10px;
  padding: 10px 20px;
  background: var(--bg-card);
  border-bottom: 1px solid var(--border-color);
  flex-wrap: wrap;
  align-items: center;
}
.stat-card {
  background: #f8fafc;
  border: 1px solid var(--border-color);
  border-radius: 8px;
  padding: 6px 16px;
  text-align: center;
  min-width: 90px;
}
.stat-value {
  font-size: 20px;
  font-weight: 700;
  line-height: 1.2;
}
.stat-label {
  font-size: 10px;
  color: #64748b;
  text-transform: uppercase;
  letter-spacing: .4px;
}
#st-pass .stat-value { color: var(--color-pass); }
#st-fail .stat-value { color: var(--color-fail); }
#st-skip .stat-value { color: var(--color-skip); }
#st-err  .stat-value { color: var(--color-error); }
/* ── Filter bar ── */
#filterBar {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
  align-items: center;
  padding: 8px 20px;
  background: var(--bg-card);
  border-bottom: 1px solid var(--border-color);
}
.status-pills {
  display: flex;
  gap: 3px;
}
.status-btn {
  border: 1px solid #cbd5e1;
  background: #fff;
  border-radius: 20px;
  padding: 2px 11px;
  font-size: 11px;
  font-weight: 600;
  cursor: pointer;
  transition: .12s;
}
.status-btn:hover { background: #f1f5f9; }
.status-btn.active { background: #1e293b; color: #fff; border-color: #1e293b; }
.status-btn.s-pass.active  { background: var(--color-pass);  border-color: var(--color-pass); }
.status-btn.s-fail.active  { background: var(--color-fail);  border-color: var(--color-fail); }
.status-btn.s-skip.active  { background: var(--color-skip);  border-color: var(--color-skip); }
.status-btn.s-error.active { background: var(--color-error); border-color: var(--color-error); }
/* ── Status badges ── */
.badge-pass  { background: var(--color-pass);  color: #fff; padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 700; }
.badge-fail  { background: var(--color-fail);  color: #fff; padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 700; }
.badge-skip  { background: var(--color-skip);  color: #fff; padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 700; }
.badge-error { background: var(--color-error); color: #fff; padding: 2px 8px; border-radius: 4px; font-size: 11px; font-weight: 700; }
/* ── Grid tweaks ── */
#main { padding: 12px 20px; }
.gridjs-container { font-size: 13px; }
.gridjs-table tr[data-st=Fail]  { background: #fff5f5; }
.gridjs-table tr[data-st=Error] { background: #fff8f0; }
.gridjs-table tr[data-st=Skip]  { background: #fafafa; }
/* ── Detail modal ── */
.modal-xl .modal-body { padding: 0; }
#detailTabs .nav-link { font-size: 12px; padding: 6px 12px; }
.tab-pane { padding: 16px; }
/* Stack trace */
.stack-wrap { position: relative; }
pre.stack-pre {
  background: #0f172a;
  color: #e2e8f0;
  border-radius: 6px;
  padding: 14px;
  font-size: 12px;
  max-height: 280px;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-word;
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
.stack-toggle-link {
  font-size: 11px;
  color: #64748b;
  cursor: pointer;
  text-decoration: underline;
  display: block;
  margin-top: 4px;
}
/* ── AI classification badges ── */
.badge-ai {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  padding: 3px 10px;
  border-radius: 4px;
  font-size: 12px;
  font-weight: 700;
  white-space: nowrap;
}
.badge-ai-productissue   { background: #fee2e2; color: #991b1b; border: 1px solid #fca5a5; }
.badge-ai-testissue      { background: #fef9c3; color: #854d0e; border: 1px solid #fde047; }
.badge-ai-flaky          { background: #ffedd5; color: #9a3412; border: 1px solid #fdba74; }
.badge-ai-infrastructure { background: #f3e8ff; color: #6b21a8; border: 1px solid #d8b4fe; }
.badge-ai-uncertain      { background: #f1f5f9; color: #475569; border: 1px solid #cbd5e1; }
/* small variant used in the grid cell */
.badge-ai-sm { font-size: 10px; padding: 1px 6px; }
/* AI analysis panel */
.ai-analysis-panel {
  background: #f0f9ff;
  border-left: 4px solid #0ea5e9;
  border-radius: 0 6px 6px 0;
  padding: 12px 16px;
  font-size: 13px;
  white-space: pre-wrap;
  word-break: break-word;
  line-height: 1.6;
}
.ai-analysis-panel .xpath-candidate {
  background: #fef3c7;
  border: 1px solid #fbbf24;
  border-radius: 3px;
  padding: 0 4px;
  font-family: monospace;
  font-size: 12px;
}
/* Screenshot */
.screenshot-wrap img {
  max-width: 100%;
  border-radius: 6px;
  border: 1px solid var(--border-color);
  cursor: zoom-in;
  transition: .15s;
}
.screenshot-wrap img:hover { box-shadow: 0 4px 20px rgba(0,0,0,.15); }
/* Screencast */
.screencast-video {
  width: 100%;
  border-radius: 6px;
  max-height: 360px;
  background: #000;
}
.screencast-link {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  color: #0d6efd;
  text-decoration: none;
  font-size: 13px;
}
.screencast-link:hover { text-decoration: underline; }
/* Custom props table */
.custom-props-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 12px;
}
.custom-props-table th {
  background: #f1f5f9;
  padding: 5px 8px;
  text-align: left;
  font-weight: 600;
  border: 1px solid var(--border-color);
}
.custom-props-table td {
  padding: 5px 8px;
  border: 1px solid var(--border-color);
  word-break: break-word;
}
/* Lightbox */
#lightbox {
  display: none;
  position: fixed;
  inset: 0;
  background: rgba(0,0,0,.85);
  z-index: 9999;
  align-items: center;
  justify-content: center;
}
#lightbox.open { display: flex; }
#lightbox img {
  max-width: 92vw;
  max-height: 90vh;
  border-radius: 6px;
  box-shadow: 0 8px 40px rgba(0,0,0,.6);
}
#lightbox-close {
  position: absolute;
  top: 14px;
  right: 20px;
  font-size: 28px;
  color: #fff;
  cursor: pointer;
  line-height: 1;
}
/* Hidden index col */
.gridjs-th:first-child,
.gridjs-td:first-child {
  width: 0 !important;
  max-width: 0;
  overflow: hidden;
  padding: 0;
  border: none;
}
/* ── Runs bar ── */
#runsBar {
  display: none;
  padding: 8px 20px;
  background: var(--bg-card);
  border-bottom: 1px solid var(--border-color);
  overflow-x: auto;
}
.runs-scroll {
  display: flex;
  gap: 8px;
  min-width: max-content;
}
.run-card {
  background: #f8fafc;
  border: 1px solid var(--border-color);
  border-radius: 8px;
  padding: 8px 14px;
  min-width: 150px;
  cursor: pointer;
  transition: .12s;
  user-select: none;
}
.run-card:hover  { border-color: #94a3b8; background: #f1f5f9; }
.run-card.active { border-color: #1e293b; background: #e2e8f0; }
.run-name {
  font-weight: 700;
  font-size: 12px;
  color: #1e293b;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  max-width: 190px;
}
.run-stats {
  font-size: 11px;
  margin-top: 3px;
  display: flex;
  gap: 6px;
  flex-wrap: wrap;
}
.run-pass-rate {
  font-size: 10px;
  color: #64748b;
  margin-top: 3px;
  text-align: right;
}
.run-progress-bar {
  height: 3px;
  border-radius: 2px;
  background: #e2e8f0;
  margin-top: 3px;
}
.run-progress-fill {
  height: 3px;
  border-radius: 2px;
}
/* ── Active view button ── */
.active-view-btn {
  background: #3b82f6 !important;
  border-color: #3b82f6 !important;
}
/* ── Tree view ── */
#tree-view { min-height: 60px; }
.suite-block {
  border: 1px solid var(--border-color);
  border-radius: 8px;
  margin-bottom: 8px;
  overflow: hidden;
  background: var(--bg-card);
}
.suite-header {
  padding: 10px 14px;
  cursor: pointer;
  background: #f8fafc;
  display: flex;
  align-items: center;
  gap: 8px;
  user-select: none;
}
.suite-header:hover { background: #f0f4f8; }
.suite-chevron {
  font-size: 11px;
  color: #94a3b8;
  width: 14px;
  display: inline-block;
  flex-shrink: 0;
  text-align: center;
}
.suite-name {
  font-weight: 600;
  font-size: 13px;
  flex: 1;
  color: #1e293b;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.suite-meta {
  color: #94a3b8;
  font-size: 11px;
  white-space: nowrap;
  flex-shrink: 0;
}
.suite-counts {
  display: flex;
  gap: 3px;
  flex-shrink: 0;
}
.suite-count {
  padding: 1px 8px;
  border-radius: 10px;
  font-size: 11px;
  font-weight: 700;
}
.suite-count.pass  { background: #dcfce7; color: #166534; }
.suite-count.fail  { background: #fee2e2; color: #991b1b; }
.suite-count.error { background: #ffedd5; color: #9a3412; }
.suite-count.skip  { background: #f1f5f9; color: #475569; }
.suite-body { display: none; }
.suite-body.open { display: block; }
.test-row {
  display: flex;
  align-items: center;
  gap: 8px;
  padding: 6px 14px 6px 28px;
  border-top: 1px solid #f1f5f9;
  cursor: pointer;
  transition: .1s;
}
.test-row:hover { background: #f8fafc; }
.test-row[data-st=Fail]  { border-left: 3px solid var(--color-fail); }
.test-row[data-st=Error] { border-left: 3px solid var(--color-error); }
.test-row[data-st=Pass]  { border-left: 3px solid var(--color-pass); }
.test-row[data-st=Skip]  { border-left: 3px solid var(--color-skip); }
.test-status-icon {
  width: 16px;
  height: 16px;
  border-radius: 50%;
  display: inline-flex;
  align-items: center;
  justify-content: center;
  font-size: 9px;
  color: #fff;
  flex-shrink: 0;
}
.test-status-icon.Pass  { background: var(--color-pass); }
.test-status-icon.Fail  { background: var(--color-fail); }
.test-status-icon.Error { background: var(--color-error); }
.test-status-icon.Skip  { background: var(--color-skip); }
.test-name-label {
  flex: 1;
  font-size: 12px;
  color: #1e293b;
  word-break: break-word;
  min-width: 0;
}
.test-duration-label {
  color: #94a3b8;
  font-size: 11px;
  white-space: nowrap;
  flex-shrink: 0;
}
.test-artifacts-list {
  display: flex;
  gap: 3px;
  flex-shrink: 0;
}
.artifact-link {
  padding: 1px 6px;
  border-radius: 3px;
  font-size: 10px;
  font-weight: 600;
  text-decoration: none;
}
.artifact-link-screenshot { background: #eff6ff; color: #1d4ed8; }
.artifact-link-video      { background: #fdf4ff; color: #7e22ce; }
.artifact-link-har        { background: #f0fdf4; color: #15803d; }
.test-detail-panel {
  background: #f8fafc;
  padding: 10px 14px 10px 28px;
  border-top: 1px solid #e2e8f0;
  display: none;
}
.test-detail-panel.open { display: block; }
.detail-tabs-row {
  display: flex;
  gap: 3px;
  margin-bottom: 8px;
  flex-wrap: wrap;
}
.detail-tab-btn {
  border: 1px solid var(--border-color);
  background: #fff;
  border-radius: 4px;
  padding: 2px 10px;
  font-size: 11px;
  cursor: pointer;
  transition: .1s;
}
.detail-tab-btn.active { background: #1e293b; color: #fff; border-color: #1e293b; }
.detail-tab-pane { display: none; }
.detail-tab-pane.active { display: block; }
.failure-box {
  background: #fef2f2;
  border-left: 3px solid var(--color-fail);
  padding: 8px 12px;
  border-radius: 0 4px 4px 0;
  font-size: 12px;
  font-family: monospace;
  margin-bottom: 8px;
  white-space: pre-wrap;
  word-break: break-word;
}
.inline-stack-box {
  font-size: 11px;
  background: #0f172a;
  color: #e2e8f0;
  border-radius: 4px;
  padding: 10px;
  max-height: 200px;
  overflow: auto;
  white-space: pre-wrap;
  word-break: break-word;
}
/* Run badge in grid */
.run-badge {
  background: #e0e7ff;
  color: #3730a3;
  padding: 1px 7px;
  border-radius: 4px;
  font-size: 11px;
  font-weight: 600;
  white-space: nowrap;
}
/* Artifact badge in grid */
.artifact-badge {
  cursor: pointer;
  font-size: 13px;
  padding: 1px 3px;
  border-radius: 3px;
  display: inline-block;
  line-height: 1;
  transition: transform 0.1s;
}
.artifact-badge:hover { transform: scale(1.25); }
/* ── Run-level blocks (3-level tree for consolidated reports) ── */
.run-block {
  border: 1px solid var(--border-color);
  border-radius: 8px;
  margin-bottom: 10px;
  overflow: hidden;
  background: var(--bg-card);
}
.run-block-header {
  padding: 11px 14px;
  cursor: pointer;
  background: #1e293b;
  color: #f1f5f9;
  display: flex;
  align-items: center;
  gap: 8px;
  user-select: none;
}
.run-block-header:hover { background: #273549; }
.run-block-chevron {
  font-size: 11px;
  color: #94a3b8;
  width: 14px;
  display: inline-block;
  flex-shrink: 0;
  text-align: center;
}
.run-block-name {
  font-weight: 700;
  font-size: 13px;
  flex: 1;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.run-block-body { display: none; padding: 8px 8px 4px; }
.run-block-body.open { display: block; }
.run-block-body .suite-block { margin-bottom: 6px; }
.run-block-body .test-row { padding-left: 42px; }
.run-block-body .test-detail-panel { padding-left: 42px; }
""";

        // ── HTML structure (with [[CSS_STYLES]] and [[JAVASCRIPT]] placeholders) ─

        private static string GetHtmlStructure() => """
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8"/>
<meta name="viewport" content="width=device-width,initial-scale=1"/>
<meta name="generator" content="SimpleSeleniumSupport">
<meta name="sss:report-name" content="[[REPORT_NAME]]">
<meta name="sss:report-type" content="[[REPORT_TYPE]]">
<title>[[REPORT_NAME]] — Test Report</title>
<link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/css/bootstrap.min.css" rel="stylesheet" crossorigin="anonymous"/>
<link href="https://unpkg.com/gridjs/dist/theme/mermaid.min.css" rel="stylesheet"/>
<style>
[[CSS_STYLES]]
</style>
</head>
<body>

<!-- ── Top bar ────────────────────────────────────────────────────────── -->
<div id="topbar">
  <h1>[[REPORT_NAME]]</h1>
  <span id="gen-time" style="color:#94a3b8;font-size:11px">Generated [[GEN_TIME]]</span>
  <div style="margin-left:auto;display:flex;align-items:center;gap:12px">
    <div style="display:flex;gap:3px">
      <button id="vbtn-table" class="btn btn-sm btn-outline-light active-view-btn" onclick="window.setView('table')">&#8862; Table</button>
      <button id="vbtn-tree"  class="btn btn-sm btn-outline-light"                 onclick="window.setView('tree')">&#8863; Tree</button>
    </div>
    <div style="display:flex;gap:8px">
      <button id="btnCsv"  class="btn btn-sm btn-outline-light">Export CSV</button>
      <button id="btnJson" class="btn btn-sm btn-outline-light">Export JSON</button>
    </div>
  </div>
</div>

<!-- ── Stats bar ──────────────────────────────────────────────────────── -->
<div id="statsBar">
  <div class="stat-card"          ><div class="stat-value" id="sv-total">&#8212;</div><div class="stat-label">Total</div></div>
  <div class="stat-card" id="st-pass"><div class="stat-value" id="sv-pass">&#8212;</div><div class="stat-label">Pass</div></div>
  <div class="stat-card" id="st-fail"><div class="stat-value" id="sv-fail">&#8212;</div><div class="stat-label">Fail</div></div>
  <div class="stat-card" id="st-skip"><div class="stat-value" id="sv-skip">&#8212;</div><div class="stat-label">Skip</div></div>
  <div class="stat-card" id="st-err" ><div class="stat-value" id="sv-err" >&#8212;</div><div class="stat-label">Error</div></div>
  <div class="stat-card"          ><div class="stat-value" id="sv-rate">&#8212;</div><div class="stat-label">Pass Rate</div></div>
  <div class="stat-card"          ><div class="stat-value" id="sv-dur" >&#8212;</div><div class="stat-label">Total Duration</div></div>
  <div class="stat-card"          ><div class="stat-value" id="sv-avg" >&#8212;</div><div class="stat-label">Avg Duration</div></div>
  <div id="load-msg" style="margin-left:auto;color:#64748b;font-size:12px">Loading&#8230;</div>
</div>

<!-- ── Runs bar (shown only when RunName is set on results) ───────────── -->
<div id="runsBar"><div class="runs-scroll" id="runsScroll"></div></div>

<!-- ── Filter bar ─────────────────────────────────────────────────────── -->
<div id="filterBar">
  <div class="status-pills">
    <button class="status-btn active" data-st="all"  >All</button>
    <button class="status-btn s-pass" data-st="Pass" >Pass</button>
    <button class="status-btn s-fail" data-st="Fail" >Fail</button>
    <button class="status-btn s-skip" data-st="Skip" >Skip</button>
    <button class="status-btn s-error"data-st="Error">Error</button>
  </div>
  <select id="f-suite"   class="form-select form-select-sm" style="width:auto;min-width:120px"><option value="">All Suites</option></select>
  <select id="f-cat"     class="form-select form-select-sm" style="width:auto;min-width:120px"><option value="">All Categories</option></select>
  <select id="f-browser" class="form-select form-select-sm" style="width:auto;min-width:120px"><option value="">All Browsers</option></select>
  <select id="f-env"     class="form-select form-select-sm" style="width:auto;min-width:110px"><option value="">All Envs</option></select>
  <select id="f-ai-class" class="form-select form-select-sm" style="display:none;width:auto;min-width:140px"><option value="">All AI Classes</option></select>
  <select id="f-run"     class="form-select form-select-sm" style="display:none;width:auto;min-width:120px"><option value="">All Runs</option></select>
  <input  id="f-search"  class="form-control form-control-sm" placeholder="Search&#8230; (press /)" style="width:200px"/>
  <select id="f-pagesize"class="form-select form-select-sm" style="width:100px"></select>
  <button id="f-clear"   class="btn btn-sm btn-outline-secondary">Clear</button>
</div>

<!-- ── Grid ───────────────────────────────────────────────────────────── -->
<div id="main"><div id="grid"></div></div>
<div id="tree-view" style="display:none;padding:0 20px 20px"></div>

<!-- ── Detail modal ───────────────────────────────────────────────────── -->
<div class="modal fade modal-xl" id="detailModal" tabindex="-1" aria-hidden="true">
  <div class="modal-dialog modal-xl modal-dialog-scrollable">
    <div class="modal-content">
      <div class="modal-header py-2">
        <h5 class="modal-title fs-6 fw-semibold" id="detailTitle">Test Detail</h5>
        <button type="button" class="btn-close" data-bs-dismiss="modal"></button>
      </div>
      <div class="modal-body p-0">
        <ul class="nav nav-tabs px-3 pt-2" id="detailTabs">
          <li class="nav-item"><button class="nav-link active" data-tab="overview"    >Overview</button></li>
          <li class="nav-item"><button class="nav-link"        data-tab="failure"     >Failure</button></li>
          <li class="nav-item"><button class="nav-link"        data-tab="ai"          >AI Analysis</button></li>
          <li class="nav-item"><button class="nav-link"        data-tab="screenshot"  >Screenshot</button></li>
          <li class="nav-item"><button class="nav-link"        data-tab="screencast"  >Screencast</button></li>
          <li class="nav-item"><button class="nav-link"        data-tab="network"     >Network</button></li>
          <li class="nav-item"><button class="nav-link"        data-tab="diagnostics" >Diagnostics</button></li>
          <li class="nav-item"><button class="nav-link"        data-tab="custom"      >Custom</button></li>
        </ul>

        <!-- Overview -->
        <div id="tab-overview" class="tab-pane">
          <table class="table table-sm table-bordered small mb-0">
            <tbody id="ov-body"></tbody>
          </table>
        </div>

        <!-- Failure -->
        <div id="tab-failure" class="tab-pane" style="display:none">
          <div id="fail-empty" class="text-muted small fst-italic">No failure details for this test.</div>
          <div id="fail-content">
            <div class="mb-3">
              <div class="fw-semibold small mb-1">Exception</div>
              <div id="fail-exc" class="bg-danger bg-opacity-10 border border-danger-subtle rounded px-3 py-2 small font-monospace"></div>
            </div>
            <div class="mb-3" id="fail-assert-wrap">
              <div class="fw-semibold small mb-1">Assertion Detail</div>
              <div id="fail-assert" class="bg-warning bg-opacity-10 border border-warning-subtle rounded px-3 py-2 small"></div>
            </div>
            <div class="mb-1 fw-semibold small">Stack Trace</div>
            <div class="stack-wrap">
              <pre class="stack-pre" id="fail-stack"></pre>
              <button class="copy-stack-btn" id="btn-copy-stack">Copy</button>
            </div>
            <span class="stack-toggle-link" id="stack-toggle">Show full stack</span>
          </div>
          <div id="skip-content" style="display:none" class="mt-2">
            <div class="fw-semibold small mb-1">Skip Reason</div>
            <div id="skip-reason" class="bg-secondary bg-opacity-10 border rounded px-3 py-2 small"></div>
          </div>
        </div>

        <!-- AI Analysis -->
        <div id="tab-ai" class="tab-pane" style="display:none">
          <div id="ai-empty" class="text-muted small fst-italic">No AI analysis available for this test.</div>
          <div id="ai-content" style="display:none">
            <div id="ai-class-header" style="display:none;margin-bottom:12px">
              <span id="ai-class-badge"></span>
            </div>
            <div id="ai-panel" class="ai-analysis-panel"></div>
          </div>
        </div>

        <!-- Screenshot -->
        <div id="tab-screenshot" class="tab-pane" style="display:none">
          <div id="ss-empty" class="text-muted small fst-italic">No screenshot attached.</div>
          <div id="ss-wrap" class="screenshot-wrap" style="display:none">
            <img id="ss-img" src="" alt="Screenshot" onclick="openLightbox(this.src)"/>
            <div class="mt-2"><a id="ss-link" href="#" target="_blank" class="btn btn-sm btn-outline-secondary">Open in new tab</a></div>
          </div>
        </div>

        <!-- Screencast -->
        <div id="tab-screencast" class="tab-pane" style="display:none">
          <div id="sc-empty" class="text-muted small fst-italic">No screencast attached.</div>
          <video id="sc-video" class="screencast-video" controls style="display:none"></video>
          <div id="sc-link-wrap" style="display:none;margin-top:8px">
            <a id="sc-link" href="#" target="_blank" class="screencast-link">
              <svg width="16" height="16" fill="currentColor" viewBox="0 0 16 16"><path d="M4.5 6.375a4.125 4.125 0 1 1 8.25 0 4.125 4.125 0 0 1-8.25 0ZM8.625 2.25a4.125 4.125 0 1 0 0 8.25 4.125 4.125 0 0 0 0-8.25Z"/></svg>
              Open Screencast Link
            </a>
          </div>
        </div>

        <!-- Network -->
        <div id="tab-network" class="tab-pane" style="display:none">
          <div id="net-empty" class="text-muted small fst-italic">No network artifacts attached.</div>
          <div id="net-content">
            <div class="mb-2" id="net-har-wrap">
              <a id="net-har" href="#" target="_blank" class="btn btn-sm btn-outline-primary">Download HAR</a>
            </div>
            <div id="net-excel-wrap">
              <a id="net-excel" href="#" target="_blank" class="btn btn-sm btn-outline-success">Download Network Excel</a>
            </div>
          </div>
        </div>

        <!-- Diagnostics -->
        <div id="tab-diagnostics" class="tab-pane" style="display:none">
          <div id="diag-empty" class="text-muted small fst-italic">No diagnostics folder attached.</div>
          <div id="diag-content" style="display:none">
            <p class="small text-muted mb-1" id="diag-path"></p>
            <a id="diag-link" href="#" target="_blank" class="btn btn-sm btn-outline-secondary">Open Diagnostics Folder</a>
          </div>
        </div>

        <!-- Custom Properties -->
        <div id="tab-custom" class="tab-pane" style="display:none">
          <div id="cp-empty" class="text-muted small fst-italic">No custom properties.</div>
          <table class="custom-props-table" id="cp-table" style="display:none">
            <thead><tr><th>Key</th><th>Value</th></tr></thead>
            <tbody id="cp-body"></tbody>
          </table>
        </div>
      </div><!-- /modal-body -->
    </div>
  </div>
</div>

<!-- ── Lightbox ───────────────────────────────────────────────────────── -->
<div id="lightbox" onclick="closeLightbox()">
  <span id="lightbox-close" onclick="closeLightbox()">&times;</span>
  <img id="lightbox-img" src="" alt="Screenshot"/>
</div>

<script src="https://cdn.jsdelivr.net/npm/bootstrap@5.3.2/dist/js/bootstrap.bundle.min.js" crossorigin="anonymous"></script>
<script src="https://unpkg.com/gridjs/dist/gridjs.umd.js"></script>
<script>(function(){
'use strict';
[[JAVASCRIPT]]
})();
</script>
</body>
</html>
""";

        // ── JavaScript body (no script tags, no IIFE wrapper) ──────────────────

        private static string GetJavaScript() => """
// ── State ──────────────────────────────────────────────────────────────
let ALL_ROWS = [];       // raw data from data.json
let FILTERED = [];       // after applying filters
let activeStatus  = 'all';
let activeSuite   = '';
let activeCat     = '';
let activeBrowser = '';
let activeEnv     = '';
let activeRun     = '';
let activeAiClass = '';
let searchTerm    = '';
let pageSize      = 50;
let gridInstance;
let currentView   = 'table';
const PAGE_SIZES = [25, 50, 100, 250, 500];
const RUNS_DATA  = [[RUNS_JSON]];          // per-run summary; empty = no run grouping
const HAS_RUNS   = RUNS_DATA.length >= 1;  // true = at least one RunName present

// ── Load ───────────────────────────────────────────────────────────────
[[DATA_INIT_SCRIPT]]

// ── Dropdowns ──────────────────────────────────────────────────────────
function populateDropdowns(){
  fillSelectWithOptions('f-suite',   getUniqueValues(ALL_ROWS, testRow => testRow.testSuite).sort());
  fillSelectWithOptions('f-cat',     getUniqueValues(ALL_ROWS, testRow => testRow.category).sort());
  fillSelectWithOptions('f-browser', getUniqueValues(ALL_ROWS, testRow => testRow.browser).sort());
  fillSelectWithOptions('f-env',     getUniqueValues(ALL_ROWS, testRow => testRow.environment).sort());
  const aiClasses = getUniqueValues(ALL_ROWS, testRow => testRow.aiClassification).sort();
  if(aiClasses.length > 0){
    fillSelectWithOptions('f-ai-class', aiClasses);
    document.getElementById('f-ai-class').style.display = '';
  }
  if(HAS_RUNS){
    const runSelectElement = document.getElementById('f-run');
    RUNS_DATA.forEach(runData => {
      const optionElement = document.createElement('option');
      optionElement.value = runData.runName;
      optionElement.textContent = runData.runName;
      runSelectElement.appendChild(optionElement);
    });
    runSelectElement.style.display = '';
    document.getElementById('runsBar').style.display = '';
    renderRunsBar();
  }
  const pageSizeSelect = document.getElementById('f-pagesize');
  PAGE_SIZES.forEach(pageSizeValue => {
    const optionElement = document.createElement('option');
    optionElement.value = pageSizeValue;
    optionElement.textContent = pageSizeValue + ' / page';
    if(pageSizeValue === pageSize) optionElement.selected = true;
    pageSizeSelect.appendChild(optionElement);
  });
}
function getUniqueValues(rows, extractFn){ return [...new Set(rows.map(extractFn).filter(Boolean))]; }
function fillSelectWithOptions(id, optionValues){
  const sel = document.getElementById(id);
  optionValues.forEach(fieldValue => {
    const optionElement = document.createElement('option');
    optionElement.value = fieldValue;
    optionElement.textContent = fieldValue;
    sel.appendChild(optionElement);
  });
}

// ── Filter ─────────────────────────────────────────────────────────────
function applyFilters(){
  const searchQuery = searchTerm.toLowerCase();
  FILTERED = ALL_ROWS.filter(testRow => {
    if(activeStatus !== 'all' && testRow.status !== activeStatus) return false;
    if(activeSuite   && testRow.testSuite   !== activeSuite)   return false;
    if(activeCat     && testRow.category    !== activeCat)     return false;
    if(activeBrowser && testRow.browser     !== activeBrowser) return false;
    if(activeEnv     && testRow.environment !== activeEnv)     return false;
    if(activeRun     && testRow.runName        !== activeRun)     return false;
    if(activeAiClass && testRow.aiClassification !== activeAiClass) return false;
    if(searchQuery && ![testRow.testName, testRow.testSuite, testRow.fullName, testRow.category, testRow.tags, testRow.browser, testRow.runName, testRow.exceptionMsg].some(fieldValue => fieldValue && fieldValue.toLowerCase().includes(searchQuery))) return false;
    return true;
  });
  renderGrid(FILTERED);
  renderStats(ALL_ROWS, FILTERED);
  if(currentView === 'tree') buildTree(FILTERED);
}

// ── Stats ──────────────────────────────────────────────────────────────
function renderStats(allRows, filteredRows){
  const passCount     = allRows.filter(testRow => testRow.status === 'Pass').length;
  const failCount     = allRows.filter(testRow => testRow.status === 'Fail').length;
  const skipCount     = allRows.filter(testRow => testRow.status === 'Skip').length;
  const errorCount    = allRows.filter(testRow => testRow.status === 'Error').length;
  const totalCount    = allRows.length;
  const totalDurationMs = allRows.reduce((accumulator, testRow) => accumulator + testRow.durationMs, 0);
  setElementText('sv-total', totalCount);
  setElementText('sv-pass',  passCount);
  setElementText('sv-fail',  failCount);
  setElementText('sv-skip',  skipCount);
  setElementText('sv-err',   errorCount);
  setElementText('sv-rate',  totalCount ? Math.round(passCount / totalCount * 100) + '%' : 'N/A');
  setElementText('sv-dur',   formatDuration(totalDurationMs));
  setElementText('sv-avg',   totalCount ? formatDuration(totalDurationMs / totalCount) : 'N/A');
}
function setElementText(id, viewName){ const treeContainer = document.getElementById(id); if(treeContainer) treeContainer.textContent = viewName; }
function formatDuration(ms){
  if(ms < 1000)  return ms.toFixed(0) + 'ms';
  if(ms < 60000) return (ms / 1000).toFixed(1) + 's';
  return (ms / 60000).toFixed(1) + 'min';
}

// ── Grid ───────────────────────────────────────────────────────────────
function renderGrid(rows){
  const data = rows.map(testRow => [
    testRow.__index,                    // 0 hidden
    '#' + (rows.indexOf(testRow) + 1),
    testRow.runName,                    // 2 run (hidden when !HAS_RUNS)
    testRow.testName,
    testRow.testSuite,
    testRow.status,
    formatDuration(testRow.durationMs),
    testRow.browser,
    testRow.tags,
    testRow.__index,                    // 9 for artifacts column
    testRow.aiClassification            // 10 for AI column
  ]);

  if(gridInstance){
    gridInstance.updateConfig({ data }).forceRender();
    return;
  }

  gridInstance = new gridjs.Grid({
    columns: [
      { id: '__idx',     name: '',          hidden: true },
      { id: 'num',       name: '#',         width: '50px',  sort: false },
      { id: 'run',       name: 'Run',       width: '110px', hidden: !HAS_RUNS,
        formatter: cellValue => gridjs.html(cellValue ? `<span class="run-badge">${htmlEscape(cellValue)}</span>` : '') },
      { id: 'name',      name: 'Test Name', width: '240px',
        formatter: (cellValue, gridRow) => gridjs.html(`<span class="cell-link" data-idx="${gridRow.cells[0].data}">${htmlEscape(cellValue)}</span>`) },
      { id: 'suite',     name: 'Suite',     width: '150px' },
      { id: 'status',    name: 'Status',    width: '80px',
        formatter: cellValue => gridjs.html(buildStatusBadge(cellValue)) },
      { id: 'dur',       name: 'Duration',  width: '90px' },
      { id: 'browser',   name: 'Browser',   width: '110px' },
      { id: 'tags',      name: 'Tags',      width: '140px',
        formatter: cellValue => gridjs.html(cellValue ? `<span class="text-muted small">${htmlEscape(cellValue)}</span>` : '') },
      { id: 'artifacts', name: 'Artifacts', width: '90px',  sort: false,
        formatter: (cellValue, gridRow) => {
          const rowIndex = gridRow.cells[0].data;
          const testData = ALL_ROWS[rowIndex];
          if(!testData) return gridjs.html('');
          const artifactBadges = [
            testData.screenshot  ? `<span class="artifact-badge" title="Screenshot"  onclick="openDetail(${rowIndex},'screenshot');event.stopPropagation()">\uD83D\uDCF7</span>` : '',
            testData.screencast  ? `<span class="artifact-badge" title="Video"       onclick="openDetail(${rowIndex},'screencast');event.stopPropagation()">\uD83C\uDFAC</span>` : '',
            testData.networkHar  ? `<span class="artifact-badge" title="Network HAR" onclick="openDetail(${rowIndex},'network');event.stopPropagation()">\uD83C\uDF10</span>` : '',
            testData.diagnostics ? `<span class="artifact-badge" title="Diagnostics" onclick="openDetail(${rowIndex},'diagnostics');event.stopPropagation()">\uD83D\uDD0D</span>` : '',
          ].filter(Boolean).join(' ');
          return gridjs.html(artifactBadges || '');
        }
      },
      { id: 'aiClass', name: 'AI', width: '110px', sort: true,
        formatter: (cellValue, gridRow) => {
          if(!cellValue) return gridjs.html('');
          const rowIndex = gridRow.cells[0].data;
          const testData = ALL_ROWS[rowIndex];
          return gridjs.html(buildAiClassBadge(cellValue, testData ? testData.aiConfidence : '', true));
        }
      }
    ],
    data,
    search: false,
    pagination: { limit: pageSize },
    sort: true,
    style: { table: { 'white-space': 'nowrap' } }
  }).render(document.getElementById('grid'));

  document.getElementById('grid').addEventListener('click', clickEvent => {
    const anchorElement = clickEvent.target.closest('.cell-link');
    if(anchorElement) openDetail(parseInt(anchorElement.dataset.idx));
  });
}

// ── Runs bar renderer ──────────────────────────────────────────────────
function renderRunsBar(){
  const scrollContainer = document.getElementById('runsScroll');
  scrollContainer.innerHTML = RUNS_DATA.map(runData => {
    const passPercentage = runData.total ? Math.round(runData.pass / runData.total * 100) : 0;
    const barColor       = passPercentage === 100 ? '#198754' : passPercentage >= 80 ? '#ffc107' : '#dc3545';
    const isActiveRun    = activeRun === runData.runName;
    return `<div class="run-card${isActiveRun ? ' active' : ''}" onclick="toggleRunFilter('${htmlEscape(runData.runName)}')">
      <div class="run-name" title="${htmlEscape(runData.runName)}">${htmlEscape(runData.runName)}</div>
      <div class="run-stats">
        <span style="color:var(--color-pass)">${runData.pass}&#10003;</span>
        ${runData.fail  ? `<span style="color:var(--color-fail)">${runData.fail}&#10007;</span>` : ''}
        ${runData.skip  ? `<span style="color:var(--color-skip)">${runData.skip}&#8856;</span>`  : ''}
        ${runData.error ? `<span style="color:var(--color-error)">${runData.error}!</span>`      : ''}
      </div>
      <div class="run-progress-bar"><div class="run-progress-fill" style="width:${passPercentage}%;background:${barColor}"></div></div>
      <div class="run-pass-rate">${passPercentage}% pass &middot; ${runData.total} test${runData.total !== 1 ? 's' : ''}</div>
    </div>`;
  }).join('');
}
window.toggleRunFilter = function(run){
  activeRun = (activeRun === run) ? '' : run;
  document.getElementById('f-run').value = activeRun;
  document.querySelectorAll('.run-card').forEach(card =>
    card.classList.toggle('active', activeRun !== '' && card.querySelector('.run-name').title === activeRun)
  );
  applyFilters();
};

function buildStatusBadge(statusValue){
  const cls = { Pass: 'pass', Fail: 'fail', Skip: 'skip', Error: 'error' }[statusValue] || 'skip';
  return `<span class="badge-${cls}">${htmlEscape(statusValue)}</span>`;
}

function buildAiClassBadge(cls, conf, small){
  const labels = {
    ProductIssue: 'Product Issue', TestIssue: 'Test Issue',
    Flaky: 'Flaky', Infrastructure: 'Infrastructure', Uncertain: 'Uncertain'
  };
  const confDot = { High: '\u25CF', Medium: '\u25D1', Low: '\u25CB' }[conf] || '';
  const sizeClass = small ? ' badge-ai-sm' : '';
  const confTitle = conf ? ` title="${htmlEscape(conf)} confidence"` : '';
  return `<span class="badge-ai badge-ai-${htmlEscape(cls.toLowerCase())}${sizeClass}">`
    + htmlEscape(labels[cls] || cls)
    + (confDot ? ` <span${confTitle}>${confDot}</span>` : '')
    + `</span>`;
}

function htmlEscape(s){ return String(s || '').replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;'); }
function toFileUrl(p){
  if(!p) return '';
  if(/^(https?|file):\/\//i.test(p)) return p;
  return 'file:///' + p.replace(/\\/g, '/');
}

// ── Detail modal ───────────────────────────────────────────────────────
const detailModalElement = document.getElementById('detailModal');
const bootstrapModal     = new bootstrap.Modal(detailModalElement);
let currentStackTraceFull = '';
let isStackTraceCollapsed = true;

window.openDetail = function(idx, initialTab){
  const testResult = ALL_ROWS[idx];
  if(!testResult) return;
  document.getElementById('detailTitle').textContent = testResult.testName + (testResult.testSuite ? ' \u00B7 ' + testResult.testSuite : '');
  // Switch to requested tab or Overview by default
  switchTab(initialTab || 'overview');

  // ── Overview ──────────────────────────────────────────────────────
  const overviewFields = [
    ['Test Name',   testResult.testName],
    ['Full Name',   testResult.fullName],
    ['Suite',       testResult.testSuite],
    ['Category',    testResult.category],
    ['Tags',        testResult.tags],
    ['Status',      buildStatusBadge(testResult.status)],
    ['Duration',    formatDuration(testResult.durationMs)],
    ['Start Time',  testResult.startTime],
    ['Browser',     testResult.browser],
    ['Environment', testResult.environment],
    ['Machine',     testResult.machineName],
  ].filter(([, fieldValue]) => fieldValue);
  const overviewBody = document.getElementById('ov-body');
  overviewBody.innerHTML = overviewFields.map(([key, fieldValue]) => `<tr><th class="text-nowrap" style="width:130px">${htmlEscape(key)}</th><td>${fieldValue}</td></tr>`).join('');

  // ── Failure tab ───────────────────────────────────────────────────
  const hasFailureInfo = testResult.exceptionType || testResult.exceptionMsg || testResult.stackTrace;
  const hasSkipReason  = testResult.skipReason;
  document.getElementById('fail-empty').style.display   = (!hasFailureInfo && !hasSkipReason) ? '' : 'none';
  document.getElementById('fail-content').style.display = hasFailureInfo ? '' : 'none';
  document.getElementById('skip-content').style.display = (!hasFailureInfo && hasSkipReason) ? '' : 'none';
  if(hasFailureInfo){
    document.getElementById('fail-exc').textContent =
      (testResult.exceptionType ? testResult.exceptionType + ': ' : '') + (testResult.exceptionMsg || '');
    const assertWrapElement = document.getElementById('fail-assert-wrap');
    if(testResult.assertMsg){
      assertWrapElement.style.display = '';
      document.getElementById('fail-assert').textContent = testResult.assertMsg;
    } else {
      assertWrapElement.style.display = 'none';
    }
    currentStackTraceFull = testResult.stackTrace || '';
    isStackTraceCollapsed = true;
    renderStackTrace();
  }
  if(hasSkipReason) document.getElementById('skip-reason').textContent = testResult.skipReason || '';

  // ── AI Analysis ───────────────────────────────────────────────────
  const hasAiContent = testResult.aiAnalysis || testResult.aiClassification;
  document.getElementById('ai-empty').style.display = hasAiContent ? 'none' : '';
  const aiContent = document.getElementById('ai-content');
  if(hasAiContent){
    aiContent.style.display = '';
    const aiClassHeader = document.getElementById('ai-class-header');
    if(testResult.aiClassification){
      aiClassHeader.style.display = '';
      document.getElementById('ai-class-badge').innerHTML =
        buildAiClassBadge(testResult.aiClassification, testResult.aiConfidence, false);
    } else {
      aiClassHeader.style.display = 'none';
    }
    const aiPanel = document.getElementById('ai-panel');
    if(testResult.aiAnalysis){
      aiPanel.style.display = '';
      aiPanel.innerHTML = renderAiAnalysisText(testResult.aiAnalysis);
    } else {
      aiPanel.style.display = 'none';
    }
  } else {
    aiContent.style.display = 'none';
  }

  // ── Screenshot ────────────────────────────────────────────────────
  document.getElementById('ss-empty').style.display = testResult.screenshot ? 'none' : '';
  const screenshotWrapper = document.getElementById('ss-wrap');
  if(testResult.screenshot){
    screenshotWrapper.style.display = '';
    document.getElementById('ss-img').src   = toFileUrl(testResult.screenshot);
    document.getElementById('ss-link').href = toFileUrl(testResult.screenshot);
  } else {
    screenshotWrapper.style.display = 'none';
  }

  // ── Screencast ────────────────────────────────────────────────────
  document.getElementById('sc-empty').style.display = testResult.screencast ? 'none' : '';
  const screencastVideoElement  = document.getElementById('sc-video');
  const screencastLinkWrapper   = document.getElementById('sc-link-wrap');
  screencastVideoElement.style.display = 'none';
  screencastLinkWrapper.style.display  = 'none';
  if(testResult.screencast){
    if(isVideoFileUrl(testResult.screencast)){
      screencastVideoElement.style.display = '';
      screencastVideoElement.src = toFileUrl(testResult.screencast);
    } else {
      screencastLinkWrapper.style.display = '';
      document.getElementById('sc-link').href = toFileUrl(testResult.screencast);
    }
  }

  // ── Network ───────────────────────────────────────────────────────
  const hasNetworkArtifacts = testResult.networkHar || testResult.networkExcel;
  document.getElementById('net-empty').style.display   = hasNetworkArtifacts ? 'none' : '';
  document.getElementById('net-content').style.display = hasNetworkArtifacts ? '' : 'none';
  if(testResult.networkHar){
    document.getElementById('net-har-wrap').style.display = '';
    document.getElementById('net-har').href = toFileUrl(testResult.networkHar);
  } else {
    document.getElementById('net-har-wrap').style.display = 'none';
  }
  if(testResult.networkExcel){
    document.getElementById('net-excel-wrap').style.display = '';
    document.getElementById('net-excel').href = toFileUrl(testResult.networkExcel);
  } else {
    document.getElementById('net-excel-wrap').style.display = 'none';
  }

  // ── Diagnostics ───────────────────────────────────────────────────
  const diagnosticsPath = testResult.diagnostics;
  document.getElementById('diag-empty').style.display   = diagnosticsPath ? 'none' : '';
  document.getElementById('diag-content').style.display = diagnosticsPath ? '' : 'none';
  if(diagnosticsPath){
    document.getElementById('diag-link').href = toFileUrl(diagnosticsPath);
    document.getElementById('diag-path').textContent = diagnosticsPath;
  }

  // ── Custom Props ─────────────────────────────────────────────────
  const customProperties = testResult.customProps && Object.keys(testResult.customProps).length > 0 ? testResult.customProps : null;
  document.getElementById('cp-empty').style.display = customProperties ? 'none' : '';
  const customPropsTable = document.getElementById('cp-table');
  if(customProperties){
    customPropsTable.style.display = '';
    document.getElementById('cp-body').innerHTML =
      Object.entries(customProperties).map(([key, fieldValue]) => `<tr><td><strong>${htmlEscape(key)}</strong></td><td>${htmlEscape(fieldValue)}</td></tr>`).join('');
  } else {
    customPropsTable.style.display = 'none';
  }

  bootstrapModal.show();
};

// ── AI text renderer ───────────────────────────────────────────────────
function renderAiAnalysisText(text){
  return text
    .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
    .replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>')
    // Highlight Suggested Fix line with a distinct style
    .replace(/^(\*\*Suggested Fix:\*\*.*)$/gm,
      '<span style="display:block;margin-top:8px;background:#f0fdf4;border-left:3px solid #22c55e;padding:4px 8px;border-radius:0 4px 4px 0">$1</span>')
    .replace(/^XPATH_CANDIDATE:\s*(.+)$/gm, (_, x) => `<span class="xpath-candidate">XPATH_CANDIDATE: ${x}</span>`)
    .replace(/\n/g, '<br>');
}

// ── Stack trace helpers ────────────────────────────────────────────────
function renderStackTrace(){
  const MAX_COLLAPSED_LENGTH = 600;
  const stackPreElement      = document.getElementById('fail-stack');
  const stackToggleElement   = document.getElementById('stack-toggle');
  if(!currentStackTraceFull){ stackPreElement.textContent = '(no stack trace)'; stackToggleElement.style.display = 'none'; return; }
  if(currentStackTraceFull.length <= MAX_COLLAPSED_LENGTH){ stackPreElement.textContent = currentStackTraceFull; stackToggleElement.style.display = 'none'; return; }
  stackPreElement.textContent = isStackTraceCollapsed ? currentStackTraceFull.substring(0, MAX_COLLAPSED_LENGTH) + '\u2026' : currentStackTraceFull;
  stackToggleElement.textContent = isStackTraceCollapsed ? 'Show full stack' : 'Collapse stack';
  stackToggleElement.style.display = '';
}

document.getElementById('stack-toggle').addEventListener('click', () => {
  isStackTraceCollapsed = !isStackTraceCollapsed; renderStackTrace();
});

document.getElementById('btn-copy-stack').addEventListener('click', () => {
  navigator.clipboard.writeText(currentStackTraceFull).then(() => {
    const tabButton = document.getElementById('btn-copy-stack');
    tabButton.textContent = 'Copied!'; setTimeout(() => tabButton.textContent = 'Copy', 1500);
  });
});

// ── Tab switching ──────────────────────────────────────────────────────
function switchTab(name){
  document.querySelectorAll('[data-tab]').forEach(tabButton => tabButton.classList.toggle('active', tabButton.dataset.tab === name));
  ['overview', 'failure', 'ai', 'screenshot', 'screencast', 'network', 'diagnostics', 'custom'].forEach(tabName => {
    document.getElementById('tab-' + tabName).style.display = tabName === name ? '' : 'none';
  });
}
document.querySelectorAll('[data-tab]').forEach(tabButton => tabButton.addEventListener('click', () => switchTab(tabButton.dataset.tab)));

// Reset video src when modal closes (stop playback)
detailModalElement.addEventListener('hidden.bs.modal', () => {
  const screencastVideoElement = document.getElementById('sc-video');
  screencastVideoElement.pause();
  screencastVideoElement.src = '';
});

// ── Screencast detection ───────────────────────────────────────────────
const VIDEO_EXTS = /\.(mp4|webm|ogv|ogg|mov)(\?.*)?$/i;
function isVideoFileUrl(url){ return VIDEO_EXTS.test(url); }

// ── Lightbox ───────────────────────────────────────────────────────────
window.openLightbox = function(src){
  document.getElementById('lightbox-img').src = src;
  document.getElementById('lightbox').classList.add('open');
};
window.closeLightbox = function(){
  document.getElementById('lightbox').classList.remove('open');
};
document.addEventListener('keydown', keyEvent => {
  if(keyEvent.key === 'Escape') closeLightbox();
});

// ── Filter event wiring ────────────────────────────────────────────────
document.querySelectorAll('.status-btn').forEach(tabButton => tabButton.addEventListener('click', () => {
  document.querySelectorAll('.status-btn').forEach(x => x.classList.remove('active'));
  tabButton.classList.add('active');
  activeStatus = tabButton.dataset.st;
  applyFilters();
}));
document.getElementById('f-suite'  ).addEventListener('change', changeEvent => { activeSuite   = changeEvent.target.value; applyFilters(); });
document.getElementById('f-cat'    ).addEventListener('change', changeEvent => { activeCat     = changeEvent.target.value; applyFilters(); });
document.getElementById('f-browser').addEventListener('change', changeEvent => { activeBrowser = changeEvent.target.value; applyFilters(); });
document.getElementById('f-env'    ).addEventListener('change', changeEvent => { activeEnv     = changeEvent.target.value; applyFilters(); });
document.getElementById('f-run'    ).addEventListener('change', changeEvent => {
  activeRun = changeEvent.target.value;
  document.querySelectorAll('.run-card').forEach(card =>
    card.classList.toggle('active', activeRun !== '' && card.querySelector('.run-name').title === activeRun)
  );
  applyFilters();
});
document.getElementById('f-search' ).addEventListener('input',  inputEvent  => { searchTerm    = inputEvent.target.value; applyFilters(); });
document.getElementById('f-pagesize').addEventListener('change', changeEvent => {
  pageSize = parseInt(changeEvent.target.value);
  if(gridInstance) gridInstance.updateConfig({ pagination: { limit: pageSize } }).forceRender();
});
document.getElementById('f-ai-class').addEventListener('change', changeEvent => { activeAiClass = changeEvent.target.value; applyFilters(); });
document.getElementById('f-clear').addEventListener('click', () => {
  activeStatus = 'all'; activeSuite = ''; activeCat = ''; activeBrowser = ''; activeEnv = ''; activeRun = ''; activeAiClass = ''; searchTerm = '';
  document.getElementById('f-search').value   = '';
  document.getElementById('f-suite').value    = '';
  document.getElementById('f-cat').value      = '';
  document.getElementById('f-browser').value  = '';
  document.getElementById('f-env').value      = '';
  document.getElementById('f-run').value      = '';
  document.getElementById('f-ai-class').value = '';
  document.querySelectorAll('.run-card').forEach(card => card.classList.remove('active'));
  document.querySelectorAll('.status-btn').forEach(tabButton => tabButton.classList.toggle('active', tabButton.dataset.st === 'all'));
  applyFilters();
});

// ── Keyboard shortcuts ─────────────────────────────────────────────────
document.addEventListener('keydown', keyEvent => {
  const tag = document.activeElement.tagName;
  if(keyEvent.key === '/' && tag !== 'INPUT' && tag !== 'TEXTAREA'){
    keyEvent.preventDefault(); document.getElementById('f-search').focus();
  }
  if(keyEvent.key === 'Escape'){
    document.getElementById('f-search').blur();
    document.getElementById('f-search').value = ''; searchTerm = ''; applyFilters();
  }
});

// ── Export helpers ─────────────────────────────────────────────────────
document.getElementById('btnCsv').addEventListener('click',  () => exportToCsv());
document.getElementById('btnJson').addEventListener('click', () => exportToJson());

function exportToCsv(){
  const columns = ['runName', 'testName', 'testSuite', 'category', 'status', 'durationMs', 'browser', 'environment', 'machineName', 'tags', 'exceptionType', 'exceptionMsg'];
  const header  = columns.join(',');
  const lines   = FILTERED.map(testRow => columns.map(key => formatCsvCell(testRow[key])).join(','));
  downloadFile('test-report.csv', header + '\n' + lines.join('\n'), 'text/csv');
}
function exportToJson(){
  downloadFile('test-report.json', JSON.stringify(FILTERED, null, 2), 'application/json');
}
function formatCsvCell(fieldValue){
  const s = String(fieldValue == null ? '' : fieldValue).replace(/"/g, '""');
  return /[",\n]/.test(s) ? `"${s}"` : s;
}
function downloadFile(name, content, type){
  const anchorElement = document.createElement('a');
  anchorElement.href = URL.createObjectURL(new Blob([content], { type }));
  anchorElement.download = name;
  anchorElement.click();
}

// ── View toggle ────────────────────────────────────────────────────────
window.setView = function(viewName){
  currentView = viewName;
  document.getElementById('main').style.display      = viewName === 'table' ? '' : 'none';
  document.getElementById('tree-view').style.display = viewName === 'tree'  ? '' : 'none';
  document.getElementById('vbtn-table').classList.toggle('active-view-btn', viewName === 'table');
  document.getElementById('vbtn-tree').classList.toggle('active-view-btn',  viewName === 'tree');
  if(viewName === 'tree') buildTree(FILTERED);
};

// ── Tree builder ────────────────────────────────────────────────────────
function buildTree(rows){
  const treeContainer = document.getElementById('tree-view');
  treeContainer.innerHTML = '';
  if(!rows.length){
    treeContainer.innerHTML = '<div style="padding:30px;text-align:center;color:#94a3b8;font-size:13px">No results match current filters.</div>';
    return;
  }
  if(HAS_RUNS){
    // 3-level: Run (source report) → Suite → Test
    const runMap = new Map();
    rows.forEach(testRow => {
      const rn = testRow.runName || '(No Run)';
      if(!runMap.has(rn)) runMap.set(rn, []);
      runMap.get(rn).push(testRow);
    });
    [...runMap.entries()]
      .sort(([, groupA], [, groupB]) => {
        const aHasFailures = groupA.some(t => t.status === 'Fail' || t.status === 'Error');
        const bHasFailures = groupB.some(t => t.status === 'Fail' || t.status === 'Error');
        return aHasFailures === bHasFailures ? 0 : aHasFailures ? -1 : 1;
      })
      .forEach(([name, tests]) => treeContainer.appendChild(makeRunBlock(name, tests)));
  } else {
    // 2-level: Suite → Test
    const suiteMap = new Map();
    rows.forEach(testRow => {
      const key = testRow.testSuite || '(No Suite)';
      if(!suiteMap.has(key)) suiteMap.set(key, []);
      suiteMap.get(key).push(testRow);
    });
    [...suiteMap.entries()]
      .sort(([, groupA], [, groupB]) => {
        const aHasFailures = groupA.some(t => t.status === 'Fail' || t.status === 'Error');
        const bHasFailures = groupB.some(t => t.status === 'Fail' || t.status === 'Error');
        return aHasFailures === bHasFailures ? 0 : aHasFailures ? -1 : 1;
      })
      .forEach(([key, tests]) => treeContainer.appendChild(makeSuiteBlock(key, tests)));
  }
}

function makeRunBlock(name, tests){
  const passCount     = tests.filter(t => t.status === 'Pass').length;
  const failCount     = tests.filter(t => t.status === 'Fail').length;
  const skipCount     = tests.filter(t => t.status === 'Skip').length;
  const errorCount    = tests.filter(t => t.status === 'Error').length;
  const totalDurationMs = tests.reduce((accumulator, t) => accumulator + t.durationMs, 0);
  const isExpanded    = failCount > 0 || errorCount > 0;
  const suiteMap = new Map();
  tests.forEach(testRow => {
    const key = testRow.testSuite || '(No Suite)';
    if(!suiteMap.has(key)) suiteMap.set(key, []);
    suiteMap.get(key).push(testRow);
  });
  const suiteBlocks = [...suiteMap.entries()]
    .sort(([, groupA], [, groupB]) => {
      const aHasFailures = groupA.some(t => t.status === 'Fail' || t.status === 'Error');
      const bHasFailures = groupB.some(t => t.status === 'Fail' || t.status === 'Error');
      return aHasFailures === bHasFailures ? 0 : aHasFailures ? -1 : 1;
    })
    .map(([key, ts]) => makeSuiteBlock(key, ts).outerHTML)
    .join('');
  const blockElement = document.createElement('div');
  blockElement.className = 'run-block';
  blockElement.innerHTML = `
    <div class="run-block-header" onclick="var blockBody=this.nextElementSibling;blockBody.classList.toggle('open');this.querySelector('.run-block-chevron').textContent=blockBody.classList.contains('open')?'\u25BC':'\u25B6'">
      <span class="run-block-chevron">${isExpanded ? '\u25BC' : '\u25B6'}</span>
      <div class="suite-counts">
        ${passCount  ? `<span class="suite-count pass">${passCount}</span>`   : ''}
        ${failCount  ? `<span class="suite-count fail">${failCount}</span>`   : ''}
        ${errorCount ? `<span class="suite-count error">${errorCount}</span>` : ''}
        ${skipCount  ? `<span class="suite-count skip">${skipCount}</span>`   : ''}
      </div>
      <span class="run-block-name" title="${htmlEscape(name)}">${htmlEscape(name)}</span>
      <span class="suite-meta" style="color:#94a3b8">${formatDuration(totalDurationMs)}&nbsp;&middot;&nbsp;${tests.length}&nbsp;test${tests.length !== 1 ? 's' : ''}</span>
    </div>
    <div class="run-block-body${isExpanded ? ' open' : ''}">
      ${suiteBlocks}
    </div>`;
  return blockElement;
}

function makeSuiteBlock(name, tests){
  const passCount     = tests.filter(t => t.status === 'Pass').length;
  const failCount     = tests.filter(t => t.status === 'Fail').length;
  const skipCount     = tests.filter(t => t.status === 'Skip').length;
  const errorCount    = tests.filter(t => t.status === 'Error').length;
  const totalDurationMs = tests.reduce((accumulator, t) => accumulator + t.durationMs, 0);
  const isExpanded    = failCount > 0 || errorCount > 0;
  const blockElement  = document.createElement('div');
  blockElement.className = 'suite-block';
  blockElement.innerHTML = `
    <div class="suite-header" onclick="var suiteBody=this.nextElementSibling;suiteBody.classList.toggle('open');this.querySelector('.suite-chevron').textContent=suiteBody.classList.contains('open')?'\u25BC':'\u25B6'">
      <span class="suite-chevron">${isExpanded ? '\u25BC' : '\u25B6'}</span>
      <div class="suite-counts">
        ${passCount  ? `<span class="suite-count pass">${passCount}</span>`   : ''}
        ${failCount  ? `<span class="suite-count fail">${failCount}</span>`   : ''}
        ${errorCount ? `<span class="suite-count error">${errorCount}</span>` : ''}
        ${skipCount  ? `<span class="suite-count skip">${skipCount}</span>`   : ''}
      </div>
      <span class="suite-name" title="${htmlEscape(name)}">${htmlEscape(name)}</span>
      <span class="suite-meta">${formatDuration(totalDurationMs)}&nbsp;&middot;&nbsp;${tests.length}&nbsp;test${tests.length !== 1 ? 's' : ''}</span>
    </div>
    <div class="suite-body${isExpanded ? ' open' : ''}">
      ${tests.map(t => makeTestRow(t)).join('')}
    </div>`;
  return blockElement;
}

function makeTestRow(testResult){
  const statusIcon   = { Pass: '\u2713', Fail: '\u2715', Skip: '\u2298', Error: '!' }[testResult.status] || '?';
  const artifactBadges = [
    testResult.screenshot ? `<a class="artifact-link artifact-link-screenshot" href="${htmlEscape(toFileUrl(testResult.screenshot))}"  target="_blank" onclick="event.stopPropagation()" title="Screenshot">\uD83D\uDCF7</a>` : '',
    testResult.screencast ? `<a class="artifact-link artifact-link-video"      href="${htmlEscape(toFileUrl(testResult.screencast))}"  target="_blank" onclick="event.stopPropagation()" title="Video">\uD83C\uDFAC</a>`      : '',
    testResult.networkHar ? `<a class="artifact-link artifact-link-har"        href="${htmlEscape(toFileUrl(testResult.networkHar))}"  target="_blank" onclick="event.stopPropagation()" title="HAR">\uD83C\uDF10</a>`        : '',
  ].join('');
  return `
    <div class="test-row" data-st="${htmlEscape(testResult.status)}" onclick="this.nextElementSibling.classList.toggle('open')">
      <span class="test-status-icon ${htmlEscape(testResult.status)}">${statusIcon}</span>
      <span class="test-name-label">${htmlEscape(testResult.testName)}</span>
      <span class="test-duration-label">${formatDuration(testResult.durationMs)}</span>
      ${artifactBadges ? `<div class="test-artifacts-list">${artifactBadges}</div>` : ''}
    </div>
    <div class="test-detail-panel">${makeTestDetail(testResult)}</div>`;
}

function makeTestDetail(testResult){
  const sections = [];
  const overviewFields = [
    ['Status',      buildStatusBadge(testResult.status)],
    ['Duration',    formatDuration(testResult.durationMs)],
    ['Suite',       testResult.testSuite],
    ['Category',    testResult.category],
    ['Tags',        testResult.tags],
    ['Browser',     testResult.browser],
    ['Environment', testResult.environment],
    ['Machine',     testResult.machineName]
  ].filter(([, fieldValue]) => fieldValue);
  sections.push({ id: 'ov', label: 'Overview', active: true,
    html: `<table style="font-size:12px;border-collapse:collapse">${overviewFields.map(([key, fieldValue]) => `<tr><td style="padding:2px 14px 2px 0;color:#64748b;white-space:nowrap">${htmlEscape(key)}</td><td style="padding:2px 0">${fieldValue}</td></tr>`).join('')}</table>` });
  if(testResult.exceptionMsg || testResult.stackTrace || testResult.assertMsg){
    sections.push({ id: 'fl', label: 'Failure', active: false,
      html: `<div class="failure-box">${htmlEscape((testResult.exceptionType ? testResult.exceptionType + ': ' : '') + testResult.exceptionMsg)}</div>
      ${testResult.assertMsg ? `<div style="background:#fffbeb;border-left:3px solid #fbbf24;padding:6px 12px;border-radius:0 4px 4px 0;font-size:12px;margin-bottom:8px">${htmlEscape(testResult.assertMsg)}</div>` : ''}
      ${testResult.stackTrace ? `<pre class="inline-stack-box">${htmlEscape(testResult.stackTrace)}</pre>` : ''}` });
  }
  if(testResult.screenshot){
    const fileUrl = toFileUrl(testResult.screenshot);
    sections.push({ id: 'ss', label: 'Screenshot', active: false,
      html: `<img src="${htmlEscape(fileUrl)}" style="max-width:100%;border-radius:4px;border:1px solid var(--border-color);cursor:zoom-in" onclick="openLightbox('${htmlEscape(fileUrl)}')" alt="screenshot"/>
      <div style="margin-top:6px"><a href="${htmlEscape(fileUrl)}" target="_blank" class="btn btn-sm btn-outline-secondary">Open full size</a></div>` });
  }
  if(testResult.screencast){
    const fileUrl = toFileUrl(testResult.screencast);
    sections.push({ id: 'vid', label: 'Video', active: false,
      html: isVideoFileUrl(testResult.screencast)
        ? `<video controls style="width:100%;max-height:280px;background:#000;border-radius:4px"><source src="${htmlEscape(fileUrl)}"/></video>`
        : `<a href="${htmlEscape(fileUrl)}" target="_blank" class="btn btn-sm btn-outline-primary">Open Video</a>` });
  }
  if(testResult.networkHar || testResult.networkExcel){
    sections.push({ id: 'net', label: 'Network', active: false,
      html: `${testResult.networkHar   ? `<a href="${htmlEscape(toFileUrl(testResult.networkHar))}"   target="_blank" class="btn btn-sm btn-outline-primary me-2">Download HAR</a>`   : ''}`
          + `${testResult.networkExcel ? `<a href="${htmlEscape(toFileUrl(testResult.networkExcel))}" target="_blank" class="btn btn-sm btn-outline-success">Download Excel</a>`       : ''}` });
  }
  if(testResult.aiAnalysis){
    sections.push({ id: 'ai', label: 'AI Analysis', active: false,
      html: `<div class="ai-analysis-panel">${renderAiAnalysisText(testResult.aiAnalysis)}</div>` });
  }
  const tabsHtml  = `<div class="detail-tabs-row">${sections.map(section => `<button class="detail-tab-btn${section.active ? ' active' : ''}" data-tdtab="${section.id}" onclick="switchTreeDetailTab(this)">${section.label}</button>`).join('')}</div>`;
  const panesHtml = sections.map(section => `<div class="detail-tab-pane${section.active ? ' active' : ''}" data-tdpane="${section.id}">${section.html}</div>`).join('');
  return tabsHtml + panesHtml;
}

window.switchTreeDetailTab = function(tabButton){
  const testDetailPanel = tabButton.closest('.test-detail-panel');
  const id = tabButton.dataset.tdtab;
  testDetailPanel.querySelectorAll('.detail-tab-btn').forEach(btn => btn.classList.toggle('active', btn.dataset.tdtab === id));
  testDetailPanel.querySelectorAll('.detail-tab-pane').forEach(pane => pane.classList.toggle('active', pane.dataset.tdpane === id));
};
""";
    }
}
