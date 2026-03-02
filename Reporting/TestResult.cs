using System;
using System.Collections.Generic;

namespace SimpleSeleniumSupport.Reporting
{
    /// <summary>Outcome of a single test case.</summary>
    public enum TestStatus
    {
        Pass,
        Fail,
        Skip,
        Error   // unexpected exception, not an assertion failure
    }

    /// <summary>
    /// Captures everything about one test execution: identity, outcome, failure details,
    /// artifacts (screenshot, screencast, HAR), and AI analysis.
    /// Designed to be populated from any test framework (NUnit, xUnit, MSTest, Selenium Grid).
    /// </summary>
    public sealed class TestResult
    {
        // ── Identity ──────────────────────────────────────────────────────────

        /// <summary>Short test method name (e.g. "LoginWithValidCredentials").</summary>
        public string TestName { get; set; } = "";

        /// <summary>Class / fixture name (e.g. "AuthTests").</summary>
        public string? TestSuite { get; set; }

        /// <summary>Fully-qualified name including namespace (e.g. "MyApp.Tests.Auth.LoginTests.LoginWithValidCredentials").</summary>
        public string? FullName { get; set; }

        /// <summary>Logical category such as "Smoke", "Regression", "E2E".</summary>
        public string? Category { get; set; }

        /// <summary>Comma-separated tags or labels (e.g. "auth,login,critical").</summary>
        public string? Tags { get; set; }

        // ── Outcome ───────────────────────────────────────────────────────────

        /// <summary>Pass / Fail / Skip / Error.</summary>
        public TestStatus Status { get; set; }

        /// <summary>Wall-clock duration of the test in milliseconds.</summary>
        public double DurationMs { get; set; }

        /// <summary>UTC time the test started.</summary>
        public DateTime StartTime { get; set; } = DateTime.UtcNow;

        // ── Environment ───────────────────────────────────────────────────────

        /// <summary>Browser name (e.g. "Chrome 124", "Firefox 125").</summary>
        public string? Browser { get; set; }

        /// <summary>Logical environment name (e.g. "QA", "Staging", "Prod").</summary>
        public string? Environment { get; set; }

        /// <summary>Machine / node name where the test ran.</summary>
        public string? MachineName { get; set; }

        // ── Failure details ───────────────────────────────────────────────────

        /// <summary>
        /// Exception type name (e.g. "NoSuchElementException", "AssertionException").
        /// Null when test passed or was skipped.
        /// </summary>
        public string? ExceptionType { get; set; }

        /// <summary>Top-level exception message.</summary>
        public string? ExceptionMessage { get; set; }

        /// <summary>
        /// Full stack trace. For assertion failures this is the assertion call stack;
        /// for unexpected errors it is the raw exception stack trace.
        /// </summary>
        public string? StackTrace { get; set; }

        /// <summary>
        /// Human-readable assertion detail (e.g. "Expected: 200 But was: 404").
        /// Populated from inner exception message or assertion framework detail.
        /// </summary>
        public string? AssertMessage { get; set; }

        /// <summary>
        /// Skip / ignore reason (e.g. "[Ignore("WIP")]" attribute value).
        /// </summary>
        public string? SkipReason { get; set; }

        // ── Artifacts ─────────────────────────────────────────────────────────

        /// <summary>
        /// Path to the failure screenshot (.png).
        /// May be relative to the report folder or an absolute path.
        /// </summary>
        public string? ScreenshotPath { get; set; }

        /// <summary>
        /// Path or URL to the test screencast/recording.
        /// Accepts: relative path, absolute path, or http/https URL.
        /// Video formats supported: .mp4, .webm, .ogv, .mov.
        /// Non-video URLs (e.g. Jira, Confluence) are rendered as a clickable link.
        /// </summary>
        public string? ScreencastPath { get; set; }

        /// <summary>Path to the folder created by <see cref="Diagnostics.FailureDiagnostics.SaveDiagnostics"/>.</summary>
        public string? DiagnosticsFolder { get; set; }

        /// <summary>Path to the HAR file captured during this test.</summary>
        public string? NetworkHarPath { get; set; }

        /// <summary>Path to the network Excel export for this test.</summary>
        public string? NetworkExcelPath { get; set; }

        // ── AI analysis ───────────────────────────────────────────────────────

        /// <summary>
        /// AI-generated failure analysis returned by
        /// <see cref="AI.AISuggestFixExtensions.AnalyzeAndSuggestFix"/>.
        /// Supports basic Markdown: **bold**, newlines, XPATH_CANDIDATE: lines.
        /// </summary>
        public string? AiAnalysis { get; set; }

        // ── Consolidation ─────────────────────────────────────────────────────

        /// <summary>
        /// Optional run label used when merging results from multiple test runs into one
        /// consolidated report (e.g. <c>"Chrome"</c>, <c>"Firefox"</c>, <c>"Regression"</c>).
        /// <para>
        /// When any result in the collection passed to
        /// <see cref="TestRunReportExporter.Export"/> has this property set, the report
        /// automatically gains a per-run summary bar, a Run column in the grid, and a
        /// Run filter dropdown.
        /// </para>
        /// </summary>
        public string? RunName { get; set; }

        // ── Custom / extensible ───────────────────────────────────────────────

        /// <summary>
        /// Arbitrary key-value pairs attached by the test (e.g. build number, sprint, user story).
        /// Rendered as a table in the detail panel.
        /// </summary>
        public Dictionary<string, string> CustomProperties { get; set; } = new();

        // ── Static factory helpers ────────────────────────────────────────────

        /// <summary>
        /// Populate failure fields from an exception in one call.
        /// </summary>
        public TestResult WithException(Exception ex)
        {
            ExceptionType    = ex.GetType().FullName ?? ex.GetType().Name;
            ExceptionMessage = ex.Message;
            StackTrace       = ex.StackTrace;
            AssertMessage    = ex.InnerException?.Message;
            Status           = IsAssertException(ex) ? TestStatus.Fail : TestStatus.Error;
            return this;
        }

        private static bool IsAssertException(Exception ex)
        {
            var t = ex.GetType().FullName ?? "";
            return t.Contains("Assert") || t.Contains("Assertion") || t.Contains("NUnit") || t.Contains("Xunit");
        }

        /// <summary>Duration helper — call at the end of the test.</summary>
        public TestResult WithDuration(DateTime startUtc)
        {
            DurationMs = (DateTime.UtcNow - startUtc).TotalMilliseconds;
            return this;
        }
    }
}
