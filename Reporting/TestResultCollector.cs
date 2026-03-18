using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSeleniumSupport.Reporting
{
    /// <summary>
    /// Thread-safe accumulator for <see cref="TestResult"/> objects collected across
    /// parallel test threads.  Designed for use with NUnit, xUnit, and MSTest parallel
    /// runners where individual tests run concurrently and a single report must be
    /// generated once all tests have finished.
    /// </summary>
    /// <remarks>
    /// <b>Typical usage pattern:</b>
    /// <code>
    /// // Shared fixture-level field (NUnit / xUnit class fixture):
    /// private readonly TestResultCollector _collector = new();
    ///
    /// // Per-test [TearDown] — safe to call from any thread simultaneously:
    /// _collector.Add(new TestResult { TestName = TestContext.CurrentContext.Test.Name, ... });
    ///
    /// // [OneTimeTearDown] — called once after all parallel tests complete:
    /// _collector.ExportSingleFile("TestReports", "My Suite");
    /// // or: _collector.Export("TestReports", "My Suite");
    /// // or: _collector.ExportToExcel("TestReports", "My Suite");
    /// // or: _collector.ExportAll("TestReports", "My Suite");   // all three at once
    /// </code>
    /// </remarks>
    public sealed class TestResultCollector
    {
        // ConcurrentBag is the right tool: unordered, no contention, write-heavy
        private readonly ConcurrentBag<TestResult> _bag = new();

        // ── Add ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Adds a single <paramref name="result"/> to the collection.
        /// Safe to call from any thread simultaneously.
        /// </summary>
        public void Add(TestResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            _bag.Add(result);
        }

        /// <summary>
        /// Adds a batch of results.  Safe to call from any thread simultaneously.
        /// </summary>
        public void AddRange(IEnumerable<TestResult> results)
        {
            if (results == null) throw new ArgumentNullException(nameof(results));
            foreach (var r in results) _bag.Add(r);
        }

        // ── Query ────────────────────────────────────────────────────────────────

        /// <summary>Number of results currently held.</summary>
        public int Count => _bag.Count;

        /// <summary>Snapshot of all collected results (no guaranteed order).</summary>
        public IReadOnlyList<TestResult> Results => _bag.ToArray();

        // ── Clear ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Removes all accumulated results.
        /// Typically called in <c>[OneTimeTearDown]</c> after exporting, so the
        /// collector can be reused across multiple test runs in the same process.
        /// </summary>
        public void Clear()
        {
            // ConcurrentBag has no Clear() in .NET 8 — swap the bag
            while (_bag.TryTake(out _)) { }
        }

        // ── Export ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Exports accumulated results to a <b>folder</b> report
        /// (<c>index.html</c> + <c>data.json</c> + <c>manifest.json</c>).
        /// </summary>
        /// <param name="outFolderRoot">Parent directory for the timestamped report folder.</param>
        /// <param name="reportName">Display name shown in the report.</param>
        /// <returns>Absolute path of the generated report folder.</returns>
        /// <param name="sanitize">
        /// Optional sanitization options. When provided, sensitive data (passwords, tokens,
        /// file paths) is scrubbed from the exported results before they are written to disk.
        /// </param>
        public string Export(string outFolderRoot, string? reportName = null,
            AI.Sanitization.SanitizationOptions? sanitize = null)
            => TestRunReportExporter.Export(Snapshot(), outFolderRoot, reportName, sanitize);

        /// <summary>
        /// Exports accumulated results to a single self-contained <c>.html</c> file.
        /// </summary>
        /// <param name="outFolderRoot">Directory where the <c>.html</c> file is written.</param>
        /// <param name="reportName">Display name shown in the report and used to name the file.</param>
        /// <param name="sanitize">Optional sanitization options to scrub sensitive data.</param>
        /// <returns>Absolute path of the generated <c>.html</c> file.</returns>
        public string ExportSingleFile(string outFolderRoot, string? reportName = null,
            AI.Sanitization.SanitizationOptions? sanitize = null)
            => TestRunReportExporter.ExportSingleFile(Snapshot(), outFolderRoot, reportName, sanitize);

        /// <summary>
        /// Exports accumulated results to an Excel <c>.xlsx</c> workbook.
        /// </summary>
        /// <param name="outFolderRoot">Directory where the <c>.xlsx</c> file is written.</param>
        /// <param name="reportName">Used to name the output file.</param>
        /// <param name="sanitize">Optional sanitization options to scrub sensitive data.</param>
        /// <returns>Absolute path of the generated <c>.xlsx</c> file.</returns>
        public string ExportToExcel(string outFolderRoot, string? reportName = null,
            AI.Sanitization.SanitizationOptions? sanitize = null)
            => TestRunReportExporter.ExportToExcel(Snapshot(), outFolderRoot, reportName, sanitize);

        /// <summary>
        /// Exports accumulated results to a JUnit 4 XML file.
        /// </summary>
        public string ExportToJUnit(string outFolderRoot, string? reportName = null)
            => TestRunReportExporter.ExportToJUnit(Snapshot(), outFolderRoot, reportName);

        /// <summary>
        /// Exports accumulated results to an NUnit 3 XML file.
        /// </summary>
        public string ExportToNUnitXml(string outFolderRoot, string? reportName = null)
            => TestRunReportExporter.ExportToNUnitXml(Snapshot(), outFolderRoot, reportName);

        /// <summary>
        /// Exports accumulated results to a Visual Studio TRX file.
        /// </summary>
        public string ExportToTrx(string outFolderRoot, string? reportName = null)
            => TestRunReportExporter.ExportToTrx(Snapshot(), outFolderRoot, reportName);

        /// <summary>
        /// Convenience method that exports to <b>all three</b> primary formats in one call:
        /// folder report, single-file HTML, and Excel.
        /// </summary>
        /// <param name="outFolderRoot">Directory where all output files/folders are written.</param>
        /// <param name="reportName">Display name used for all three exports.</param>
        /// <returns>
        /// Tuple of the three generated paths:
        /// (<c>folderPath</c>, <c>htmlPath</c>, <c>xlsxPath</c>).
        /// </returns>
        /// <param name="sanitize">
        /// Optional sanitization options. When provided, sensitive data is scrubbed from
        /// all three output formats before they are written to disk.
        /// </param>
        public (string FolderPath, string HtmlPath, string XlsxPath) ExportAll(
            string outFolderRoot, string? reportName = null,
            AI.Sanitization.SanitizationOptions? sanitize = null)
        {
            // Snapshot once — all three exports see the same ordered list
            var results = Snapshot();
            var folder  = TestRunReportExporter.Export(results, outFolderRoot, reportName, sanitize);
            var html    = TestRunReportExporter.ExportSingleFile(results, outFolderRoot, reportName, sanitize);
            var xlsx    = TestRunReportExporter.ExportToExcel(results, outFolderRoot, reportName, sanitize);
            return (folder, html, xlsx);
        }

        // ── Shared singleton (optional convenience) ──────────────────────────────

        /// <summary>
        /// Optional process-wide shared collector.
        /// Useful when all tests in a process share a single fixture and you don't want
        /// to pass the collector instance around.
        /// Reset with <see cref="ResetShared"/> between independent test runs.
        /// </summary>
        public static TestResultCollector Shared { get; private set; } = new();

        /// <summary>
        /// Replaces <see cref="Shared"/> with a fresh empty collector and returns the old one
        /// (so you can still export from it after resetting).
        /// </summary>
        public static TestResultCollector ResetShared()
        {
            var old = Shared;
            Shared = new TestResultCollector();
            return old;
        }

        // ── Private helpers ──────────────────────────────────────────────────────

        // Materialise to a stable ordered List: StartTime ascending, TestName as tiebreaker.
        // ConcurrentBag.ToList() is LIFO-per-thread with no stable order guarantee.
        private List<TestResult> Snapshot()
            => _bag.OrderBy(r => r.StartTime).ThenBy(r => r.TestName).ToList();
    }
}
