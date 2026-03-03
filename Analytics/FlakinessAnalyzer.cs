namespace SimpleSeleniumSupport.Analytics
{
    /// <summary>Flakiness metrics for a single test across multiple runs.</summary>
    public sealed class FlakyTest
    {
        public string TestName       { get; set; } = "";
        public string? FullName      { get; set; }
        /// <summary>0.0 = always consistent, 1.0 = alternates pass/fail every run.</summary>
        public double FlakinessScore { get; set; }
        public int    FailCount      { get; set; }
        public int    PassCount      { get; set; }
        public int    TotalRuns      { get; set; }
        /// <summary>Status strings from the most recent 10 runs (oldest → newest).</summary>
        public List<string> RecentStatuses { get; set; } = new();
    }

    /// <summary>Flakiness report computed from multi-run history.</summary>
    public sealed class FlakinessReport
    {
        public List<FlakyTest> FlakyTests { get; set; } = new();
        /// <summary>Pass-rate trend: (Timestamp, PassRate%) per run, ordered by time.</summary>
        public List<(DateTime Timestamp, double PassRate)> PassRateTrend { get; set; } = new();
        public int RunsAnalysed { get; set; }
    }

    /// <summary>
    /// Computes per-test flakiness scores and pass-rate trends from
    /// a collection of <see cref="TestRunRecord"/> history entries.
    /// </summary>
    public static class FlakinessAnalyzer
    {
        /// <summary>
        /// Analyses the provided run history and returns a <see cref="FlakinessReport"/>.
        /// </summary>
        /// <param name="history">
        /// Ordered list of run records (typically loaded from <see cref="HistoryStore.Load"/>).
        /// </param>
        /// <param name="minimumRuns">
        /// Minimum number of runs a test must appear in before a flakiness score is computed.
        /// Defaults to 2.
        /// </param>
        public static FlakinessReport Analyse(
            IReadOnlyList<TestRunRecord> history,
            int minimumRuns = 2)
        {
            var ordered = history.OrderBy(r => r.Timestamp).ToList();

            // Group all result summaries by test full-name (fallback to test name)
            var byTest = new Dictionary<string, List<(DateTime When, string Status)>>(StringComparer.OrdinalIgnoreCase);

            foreach (var run in ordered)
            {
                foreach (var r in run.Results)
                {
                    var key = r.FullName ?? r.TestName;
                    if (!byTest.TryGetValue(key, out var lst))
                        byTest[key] = lst = new();
                    lst.Add((run.Timestamp, r.Status));
                }
            }

            var flaky = new List<FlakyTest>();
            foreach (var (key, entries) in byTest)
            {
                if (entries.Count < minimumRuns) continue;

                var statuses = entries.OrderBy(e => e.When).Select(e => e.Status).ToList();
                var transitions = 0;
                for (int i = 1; i < statuses.Count; i++)
                {
                    bool prevPass = IsPass(statuses[i - 1]);
                    bool currPass = IsPass(statuses[i]);
                    if (prevPass != currPass) transitions++;
                }

                double score = statuses.Count > 1 ? (double)transitions / (statuses.Count - 1) : 0;
                var first = byTest.First(kv => kv.Key == key);

                flaky.Add(new FlakyTest
                {
                    TestName       = entries[0].Status.Length > 0 ? key.Split('.').Last() : key,
                    FullName       = key,
                    FlakinessScore = Math.Round(score, 4),
                    PassCount      = statuses.Count(IsPass),
                    FailCount      = statuses.Count(s => !IsPass(s)),
                    TotalRuns      = statuses.Count,
                    RecentStatuses = statuses.TakeLast(10).ToList()
                });
            }

            var trend = ordered.Select(r => (r.Timestamp, r.Statistics.PassRate)).ToList();

            return new FlakinessReport
            {
                FlakyTests    = flaky.OrderByDescending(f => f.FlakinessScore).ToList(),
                PassRateTrend = trend,
                RunsAnalysed  = ordered.Count
            };
        }

        private static bool IsPass(string status) =>
            status.Equals("Pass", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("Passed", StringComparison.OrdinalIgnoreCase);
    }
}
