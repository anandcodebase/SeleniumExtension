using SimpleSeleniumSupport.Reporting;
using System.Xml.Linq;

namespace SimpleSeleniumSupport.CiExport
{
    /// <summary>
    /// Exports <see cref="TestResult"/> collections to Visual Studio TRX format (.trx).
    /// <para>
    /// Compatible with Azure DevOps test result publishing and <c>dotnet test</c> reporting.
    /// </para>
    /// </summary>
    public static class TrxExporter
    {
        private static readonly XNamespace Ns = "http://microsoft.com/schemas/VisualStudio/TeamTest/2010";

        /// <summary>
        /// Exports results to a TRX file.
        /// </summary>
        /// <param name="results">Test results to export.</param>
        /// <param name="outputPath">Full path to the output .trx file.</param>
        /// <param name="runName">Optional run label. Defaults to "Test Run".</param>
        public static void Export(
            IEnumerable<TestResult> results,
            string outputPath,
            string runName = "Test Run")
        {
            var list = results.ToList();

            // Assign deterministic GUIDs based on test name
            var ids = list.ToDictionary(
                r => r,
                r => Guid.NewGuid().ToString("D"));

            var total   = list.Count;
            var passed  = list.Count(r => r.Status == TestStatus.Pass);
            var failed  = list.Count(r => r.Status is TestStatus.Fail or TestStatus.Error);
            var skipped = list.Count(r => r.Status == TestStatus.Skip);
            var runId   = Guid.NewGuid().ToString("D");

            var testDefinitions = new XElement(Ns + "TestDefinitions");
            var results_el      = new XElement(Ns + "Results");

            foreach (var r in list)
            {
                var id = ids[r];
                var className = r.TestSuite ?? "UnknownSuite";

                testDefinitions.Add(new XElement(Ns + "UnitTest",
                    new XAttribute("name", r.TestName),
                    new XAttribute("id",   id),
                    new XElement(Ns + "TestMethod",
                        new XAttribute("className", className),
                        new XAttribute("name",      r.TestName))));

                var outcome = r.Status switch
                {
                    TestStatus.Pass  => "Passed",
                    TestStatus.Fail  => "Failed",
                    TestStatus.Error => "Failed",
                    TestStatus.Skip  => "NotExecuted",
                    _                => "Inconclusive"
                };

                var duration = TimeSpan.FromMilliseconds(r.DurationMs)
                    .ToString("hh\\:mm\\:ss\\.fffffff");
                var endTime = r.StartTime.AddMilliseconds(r.DurationMs);

                var resultEl = new XElement(Ns + "UnitTestResult",
                    new XAttribute("testId",      id),
                    new XAttribute("testName",    r.TestName),
                    new XAttribute("computerName", r.MachineName ?? Environment.MachineName),
                    new XAttribute("duration",    duration),
                    new XAttribute("startTime",   r.StartTime.ToString("O")),
                    new XAttribute("endTime",     endTime.ToString("O")),
                    new XAttribute("outcome",     outcome));

                if (r.Status is TestStatus.Fail or TestStatus.Error)
                {
                    resultEl.Add(new XElement(Ns + "Output",
                        new XElement(Ns + "ErrorInfo",
                            new XElement(Ns + "Message",    r.ExceptionMessage ?? ""),
                            new XElement(Ns + "StackTrace", r.StackTrace ?? ""))));
                }
                else if (r.Status == TestStatus.Skip)
                {
                    resultEl.Add(new XElement(Ns + "Output",
                        new XElement(Ns + "StdOut", r.SkipReason ?? "")));
                }

                if (!string.IsNullOrWhiteSpace(r.AiAnalysis))
                {
                    var existing = resultEl.Element(Ns + "Output")
                        ?? new XElement(Ns + "Output");
                    if (!resultEl.Elements(Ns + "Output").Any())
                        resultEl.Add(existing);
                    existing.Add(new XElement(Ns + "StdOut", $"AI Analysis:\n{r.AiAnalysis}"));
                }

                results_el.Add(resultEl);
            }

            var resultSummary = new XElement(Ns + "ResultSummary",
                new XAttribute("outcome", failed > 0 ? "Failed" : "Completed"),
                new XElement(Ns + "Counters",
                    new XAttribute("total",           total),
                    new XAttribute("executed",        total - skipped),
                    new XAttribute("passed",          passed),
                    new XAttribute("failed",          failed),
                    new XAttribute("notExecuted",     skipped),
                    new XAttribute("error",           list.Count(r => r.Status == TestStatus.Error)),
                    new XAttribute("inconclusive",    0)));

            var testRun = new XElement(Ns + "TestRun",
                new XAttribute("id",   runId),
                new XAttribute("name", runName),
                testDefinitions,
                results_el,
                resultSummary);

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            new XDocument(new XDeclaration("1.0", "utf-8", null), testRun)
                .Save(outputPath);
        }
    }
}
