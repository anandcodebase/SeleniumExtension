using SimpleSeleniumSupport.Reporting;
using System.Text.Json;

namespace SimpleSeleniumSupport.Analytics
{
    /// <summary>
    /// Reads and writes a <c>test-history.json</c> file that accumulates
    /// <see cref="TestRunRecord"/> entries across test runs.
    /// <para>
    /// Thread-safe via a static lock. Configure via
    /// <see cref="SimpleSeleniumSupportDefaults.AnalyticsHistoryDirectory"/> and
    /// <see cref="SimpleSeleniumSupportDefaults.AnalyticsMaxRunsKept"/>.
    /// </para>
    /// </summary>
    public static class HistoryStore
    {
        private static readonly object _lock = new();
        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            WriteIndented        = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        private static string HistoryFile =>
            Path.Combine(SimpleSeleniumSupportDefaults.AnalyticsHistoryDirectory, "test-history.json");

        /// <summary>
        /// Appends a new run to the history file.
        /// Older runs are pruned to <see cref="SimpleSeleniumSupportDefaults.AnalyticsMaxRunsKept"/>.
        /// </summary>
        public static void Append(IEnumerable<TestResult> results, string runName = "Test Run")
        {
            var summaries = results.Select(TestResultSummary.From).ToList();
            var record = new TestRunRecord
            {
                RunName    = runName,
                Timestamp  = DateTime.UtcNow,
                Results    = summaries,
                Statistics = RunStatistics.From(summaries)
            };

            lock (_lock)
            {
                Directory.CreateDirectory(SimpleSeleniumSupportDefaults.AnalyticsHistoryDirectory);
                var existing = Load();
                existing.Add(record);

                // Trim old entries
                var max = SimpleSeleniumSupportDefaults.AnalyticsMaxRunsKept;
                if (existing.Count > max)
                    existing = existing.OrderByDescending(r => r.Timestamp).Take(max).ToList();

                File.WriteAllText(HistoryFile, JsonSerializer.Serialize(existing, _jsonOpts));
            }
        }

        /// <summary>Loads all persisted run records, ordered oldest-first.</summary>
        public static List<TestRunRecord> Load()
        {
            if (!File.Exists(HistoryFile)) return new();
            try
            {
                var json = File.ReadAllText(HistoryFile);
                return JsonSerializer.Deserialize<List<TestRunRecord>>(json, _jsonOpts) ?? new();
            }
            catch { return new(); }
        }
    }
}
