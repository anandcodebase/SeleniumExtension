using SimpleSeleniumSupport.Reporting;
using System.Text.Json;

namespace SimpleSeleniumSupport.Notifications
{
    /// <summary>
    /// Writes Allure 2 JSON result files to an output directory.
    /// <para>
    /// Each <see cref="TestResult"/> is written as a <c>{uuid}-result.json</c> file
    /// compatible with the <c>allure generate</c> CLI and Allure TestOps.
    /// No Allure SDK NuGet package is required — this is pure JSON file writing.
    /// </para>
    /// </summary>
    public static class AllureJsonExporter
    {
        private static readonly JsonSerializerOptions _opts = new()
        {
            WriteIndented        = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        /// <summary>
        /// Exports all results as Allure 2 result files.
        /// </summary>
        /// <param name="results">Test results to export.</param>
        /// <param name="outputDirectory">
        /// Directory where the <c>{uuid}-result.json</c> files are written.
        /// Typically named <c>allure-results</c>.
        /// </param>
        public static void Export(IEnumerable<TestResult> results, string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);

            foreach (var r in results)
            {
                var uuid   = Guid.NewGuid().ToString("D");
                var start  = new DateTimeOffset(r.StartTime).ToUnixTimeMilliseconds();
                var stop   = new DateTimeOffset(r.StartTime.AddMilliseconds(r.DurationMs)).ToUnixTimeMilliseconds();

                var status = r.Status switch
                {
                    TestStatus.Pass  => "passed",
                    TestStatus.Fail  => "failed",
                    TestStatus.Error => "broken",
                    TestStatus.Skip  => "skipped",
                    _                => "unknown"
                };

                var labels = new List<object>
                {
                    new { name = "suite",       value = r.TestSuite   ?? "Default" },
                    new { name = "testClass",   value = r.FullName    ?? r.TestName },
                    new { name = "severity",    value = r.Status == TestStatus.Fail ? "critical" : "normal" },
                    new { name = "framework",   value = "SimpleSeleniumSupport" }
                };

                if (!string.IsNullOrWhiteSpace(r.Category))
                    labels.Add(new { name = "epic", value = r.Category! });
                if (!string.IsNullOrWhiteSpace(r.Tags))
                    foreach (var tag in r.Tags!.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        labels.Add(new { name = "tag", value = tag });
                if (!string.IsNullOrWhiteSpace(r.Browser))
                    labels.Add(new { name = "browser", value = r.Browser! });

                var allureResult = new
                {
                    uuid,
                    name        = r.TestName,
                    fullName    = r.FullName ?? r.TestName,
                    status,
                    start,
                    stop,
                    labels,
                    statusDetails = r.Status is TestStatus.Fail or TestStatus.Error
                        ? new { message = r.ExceptionMessage ?? "", trace = r.StackTrace ?? "" }
                        : null as object,
                    description = r.AiAnalysis
                };

                var path = Path.Combine(outputDirectory, $"{uuid}-result.json");
                File.WriteAllText(path, JsonSerializer.Serialize(allureResult, _opts));
            }
        }
    }
}
