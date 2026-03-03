using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SimpleSeleniumSupport.Analytics
{
    /// <summary>
    /// Generates a self-contained <c>analytics.html</c> file showing multi-run trends,
    /// flakiness rankings, and status distributions using Chart.js (loaded from CDN).
    /// </summary>
    public static class AnalyticsDashboardExporter
    {
        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        /// <summary>
        /// Exports an analytics dashboard HTML to the specified path.
        /// </summary>
        /// <param name="history">Run history loaded from <see cref="HistoryStore.Load"/>.</param>
        /// <param name="outputPath">Full path for the output <c>.html</c> file.</param>
        /// <param name="dashboardTitle">Title shown in the browser tab and page header.</param>
        public static void Export(
            IReadOnlyList<TestRunRecord> history,
            string outputPath,
            string dashboardTitle = "Test Analytics Dashboard")
        {
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var report  = FlakinessAnalyzer.Analyse(history);
            var ordered = history.OrderBy(h => h.Timestamp).ToList();

            // Prepare chart datasets
            var runLabels   = ordered.Select(r => r.Timestamp.ToString("MM/dd HH:mm")).ToList();
            var passRates   = ordered.Select(r => Math.Round(r.Statistics.PassRate, 1)).ToList();
            var medians     = ordered.Select(r => Math.Round(r.Statistics.MedianDurationMs, 0)).ToList();
            var p95s        = ordered.Select(r => Math.Round(r.Statistics.P95DurationMs, 0)).ToList();
            var topFlaky    = report.FlakyTests.Take(10).ToList();
            var flakyNames  = topFlaky.Select(f => f.TestName.Length > 30 ? f.TestName[..30] + "…" : f.TestName).ToList();
            var flakyScores = topFlaky.Select(f => Math.Round(f.FlakinessScore * 100, 1)).ToList();

            // Latest run status distribution
            var latest    = ordered.LastOrDefault();
            var latestStats = latest?.Statistics ?? new RunStatistics();

            var runLabelsJson   = JsonSerializer.Serialize(runLabels,   _jsonOpts);
            var passRatesJson   = JsonSerializer.Serialize(passRates,   _jsonOpts);
            var mediansJson     = JsonSerializer.Serialize(medians,     _jsonOpts);
            var p95sJson        = JsonSerializer.Serialize(p95s,        _jsonOpts);
            var flakyNamesJson  = JsonSerializer.Serialize(flakyNames,  _jsonOpts);
            var flakyScoresJson = JsonSerializer.Serialize(flakyScores, _jsonOpts);

            var html = GetTemplate()
                .Replace("[[TITLE]]",         dashboardTitle)
                .Replace("[[RUN_COUNT]]",      ordered.Count.ToString())
                .Replace("[[TOTAL_TESTS]]",    latestStats.TotalTests.ToString())
                .Replace("[[PASS_RATE]]",      latestStats.PassRate.ToString("F1"))
                .Replace("[[FLAKY_COUNT]]",    report.FlakyTests.Count(f => f.FlakinessScore > 0.2).ToString())
                .Replace("[[RUN_LABELS]]",     runLabelsJson)
                .Replace("[[PASS_RATES]]",     passRatesJson)
                .Replace("[[MEDIANS]]",        mediansJson)
                .Replace("[[P95S]]",           p95sJson)
                .Replace("[[FLAKY_NAMES]]",    flakyNamesJson)
                .Replace("[[FLAKY_SCORES]]",   flakyScoresJson)
                .Replace("[[PASSED]]",         latestStats.Passed.ToString())
                .Replace("[[FAILED]]",         latestStats.Failed.ToString())
                .Replace("[[SKIPPED]]",        latestStats.Skipped.ToString());

            File.WriteAllText(outputPath, html, Encoding.UTF8);
        }

        private static string GetTemplate() => """
<!DOCTYPE html>
<html lang="en">
<head>
<meta charset="UTF-8">
<title>[[TITLE]]</title>
<script src="https://cdn.jsdelivr.net/npm/chart.js@4"></script>
<style>
  * { box-sizing: border-box; margin: 0; padding: 0; }
  body { font-family: system-ui, sans-serif; background: #f5f5f5; color: #222; }
  header { background: #1e3a5f; color: #fff; padding: 16px 24px; }
  header h1 { font-size: 1.4rem; }
  .stats { display: flex; gap: 16px; padding: 16px 24px; flex-wrap: wrap; }
  .stat { background: #fff; border-radius: 8px; padding: 16px 24px; min-width: 140px; box-shadow: 0 1px 3px rgba(0,0,0,.12); }
  .stat .val { font-size: 2rem; font-weight: 700; color: #1e3a5f; }
  .stat .lbl { font-size: .75rem; color: #666; margin-top: 4px; }
  .charts { display: grid; grid-template-columns: 1fr 1fr; gap: 16px; padding: 0 24px 24px; }
  .chart-card { background: #fff; border-radius: 8px; padding: 20px; box-shadow: 0 1px 3px rgba(0,0,0,.12); }
  .chart-card h2 { font-size: .9rem; color: #444; margin-bottom: 12px; }
  canvas { max-height: 280px; }
  @media (max-width: 768px) { .charts { grid-template-columns: 1fr; } }
</style>
</head>
<body>
<header>
  <h1>[[TITLE]]</h1>
  <div style="font-size:.85rem;opacity:.8;margin-top:4px">Analysed [[RUN_COUNT]] run(s)</div>
</header>

<div class="stats">
  <div class="stat"><div class="val">[[TOTAL_TESTS]]</div><div class="lbl">Tests (latest run)</div></div>
  <div class="stat"><div class="val">[[PASS_RATE]]%</div><div class="lbl">Pass Rate (latest)</div></div>
  <div class="stat"><div class="val">[[FLAKY_COUNT]]</div><div class="lbl">Flaky Tests (&gt;20%)</div></div>
  <div class="stat"><div class="val">[[RUN_COUNT]]</div><div class="lbl">Runs Analysed</div></div>
</div>

<div class="charts">
  <div class="chart-card">
    <h2>Pass Rate Trend</h2>
    <canvas id="passRateChart"></canvas>
  </div>
  <div class="chart-card">
    <h2>Duration Trend (Median &amp; P95 ms)</h2>
    <canvas id="durationChart"></canvas>
  </div>
  <div class="chart-card">
    <h2>Top Flaky Tests (score %)</h2>
    <canvas id="flakyChart"></canvas>
  </div>
  <div class="chart-card">
    <h2>Latest Run — Status Distribution</h2>
    <canvas id="statusChart"></canvas>
  </div>
</div>

<script>
const labels = [[RUN_LABELS]];
const passRates = [[PASS_RATES]];
const medians   = [[MEDIANS]];
const p95s      = [[P95S]];
const flakyNames  = [[FLAKY_NAMES]];
const flakyScores = [[FLAKY_SCORES]];

Chart.defaults.font.size = 11;

new Chart(document.getElementById('passRateChart'), {
  type: 'line',
  data: {
    labels,
    datasets: [{ label: 'Pass Rate %', data: passRates, borderColor: '#22c55e', backgroundColor: 'rgba(34,197,94,.1)', fill: true, tension: 0.3 }]
  },
  options: { scales: { y: { min: 0, max: 100 } } }
});

new Chart(document.getElementById('durationChart'), {
  type: 'line',
  data: {
    labels,
    datasets: [
      { label: 'Median ms', data: medians, borderColor: '#3b82f6', tension: 0.3 },
      { label: 'P95 ms',    data: p95s,    borderColor: '#f59e0b', borderDash: [4,4], tension: 0.3 }
    ]
  }
});

new Chart(document.getElementById('flakyChart'), {
  type: 'bar',
  data: {
    labels: flakyNames,
    datasets: [{ label: 'Flakiness %', data: flakyScores, backgroundColor: '#ef4444' }]
  },
  options: { indexAxis: 'y', scales: { x: { min: 0, max: 100 } } }
});

new Chart(document.getElementById('statusChart'), {
  type: 'doughnut',
  data: {
    labels: ['Passed', 'Failed / Error', 'Skipped'],
    datasets: [{ data: [[[PASSED]], [[FAILED]], [[SKIPPED]]], backgroundColor: ['#22c55e','#ef4444','#94a3b8'] }]
  }
});
</script>
</body>
</html>
""";
    }
}
