using SimpleSeleniumSupport.Reporting;

namespace SimpleSeleniumSupport.Analytics
{
    /// <summary>
    /// Serialisable record of one completed test run, persisted to the history store
    /// for cross-run analytics and flakiness tracking.
    /// </summary>
    public sealed class TestRunRecord
    {
        public Guid   RunId     { get; set; } = Guid.NewGuid();
        public string RunName   { get; set; } = "Test Run";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public List<TestResultSummary> Results  { get; set; } = new();
        public RunStatistics Statistics          { get; set; } = new();
    }

    /// <summary>Lightweight summary of a single test result for history storage.</summary>
    public sealed class TestResultSummary
    {
        public string TestName   { get; set; } = "";
        public string? FullName  { get; set; }
        public string? TestSuite { get; set; }
        public string Status     { get; set; } = "Pass";  // Pass|Fail|Error|Skip
        public double DurationMs { get; set; }
        public int    RetryCount { get; set; }
        public string? Browser   { get; set; }
        public string? Environment { get; set; }

        internal static TestResultSummary From(TestResult r) => new()
        {
            TestName    = r.TestName,
            FullName    = r.FullName,
            TestSuite   = r.TestSuite,
            Status      = r.Status.ToString(),
            DurationMs  = r.DurationMs,
            RetryCount  = r.RetryCount,
            Browser     = r.Browser,
            Environment = r.Environment
        };
    }
}
