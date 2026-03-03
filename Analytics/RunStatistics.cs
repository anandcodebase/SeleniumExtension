namespace SimpleSeleniumSupport.Analytics
{
    /// <summary>Aggregated performance statistics for a single test run.</summary>
    public sealed class RunStatistics
    {
        public int    TotalTests       { get; set; }
        public int    Passed           { get; set; }
        public int    Failed           { get; set; }
        public int    Skipped          { get; set; }
        public double PassRate         { get; set; }   // 0.0–100.0
        public double MinDurationMs    { get; set; }
        public double MaxDurationMs    { get; set; }
        public double MedianDurationMs { get; set; }
        public double P95DurationMs    { get; set; }
        public double TotalDurationMs  { get; set; }

        internal static RunStatistics From(IReadOnlyList<TestResultSummary> results)
        {
            var total   = results.Count;
            var passed  = results.Count(r => r.Status == "Pass");
            var failed  = results.Count(r => r.Status is "Fail" or "Error");
            var skipped = results.Count(r => r.Status == "Skip");
            var durations = results.Select(r => r.DurationMs).OrderBy(d => d).ToList();

            return new RunStatistics
            {
                TotalTests       = total,
                Passed           = passed,
                Failed           = failed,
                Skipped          = skipped,
                PassRate         = total > 0 ? (passed / (double)total) * 100.0 : 0,
                MinDurationMs    = durations.Count > 0 ? durations[0] : 0,
                MaxDurationMs    = durations.Count > 0 ? durations[^1] : 0,
                MedianDurationMs = Percentile(durations, 50),
                P95DurationMs    = Percentile(durations, 95),
                TotalDurationMs  = durations.Sum()
            };
        }

        private static double Percentile(List<double> sorted, int p)
        {
            if (sorted.Count == 0) return 0;
            var idx = (int)Math.Ceiling(p / 100.0 * sorted.Count) - 1;
            return sorted[Math.Max(0, Math.Min(idx, sorted.Count - 1))];
        }
    }
}
