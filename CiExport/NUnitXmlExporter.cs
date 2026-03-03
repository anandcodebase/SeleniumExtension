using SimpleSeleniumSupport.Reporting;
using System.Xml.Linq;

namespace SimpleSeleniumSupport.CiExport
{
    /// <summary>
    /// Exports <see cref="TestResult"/> collections to NUnit 3 XML format.
    /// <para>
    /// Compatible with Azure DevOps, TeamCity, and the NUnit Console test runner.
    /// </para>
    /// </summary>
    public static class NUnitXmlExporter
    {
        /// <summary>
        /// Exports results to an NUnit 3 XML file.
        /// </summary>
        /// <param name="results">Test results to export.</param>
        /// <param name="outputPath">Full path to the output .xml file.</param>
        /// <param name="runName">Optional run label. Defaults to "Test Run".</param>
        public static void Export(
            IEnumerable<TestResult> results,
            string outputPath,
            string runName = "Test Run")
        {
            var list     = results.ToList();
            var total    = list.Count;
            var passed   = list.Count(r => r.Status == TestStatus.Pass);
            var failed   = list.Count(r => r.Status is TestStatus.Fail or TestStatus.Error);
            var skipped  = list.Count(r => r.Status == TestStatus.Skip);
            var runResult = failed > 0 ? "Failed" : "Passed";
            var startTime = list.Min(r => r.StartTime);
            var durationSec = list.Sum(r => r.DurationMs) / 1000.0;

            var testRun = new XElement("test-run",
                new XAttribute("id",        "1"),
                new XAttribute("name",      runName),
                new XAttribute("total",     total),
                new XAttribute("passed",    passed),
                new XAttribute("failed",    failed),
                new XAttribute("skipped",   skipped),
                new XAttribute("result",    runResult),
                new XAttribute("start-time", startTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffff")),
                new XAttribute("duration",  durationSec.ToString("F3")));

            foreach (var group in list.GroupBy(r => r.TestSuite ?? "Default"))
            {
                var gPassed   = group.Count(r => r.Status == TestStatus.Pass);
                var gFailed   = group.Count(r => r.Status is TestStatus.Fail or TestStatus.Error);
                var gSkipped  = group.Count(r => r.Status == TestStatus.Skip);
                var gResult   = gFailed > 0 ? "Failed" : "Passed";
                var gStart    = group.Min(r => r.StartTime);
                var gDuration = group.Sum(r => r.DurationMs) / 1000.0;

                var fixture = new XElement("test-suite",
                    new XAttribute("type",       "TestFixture"),
                    new XAttribute("name",       group.Key),
                    new XAttribute("total",      group.Count()),
                    new XAttribute("passed",     gPassed),
                    new XAttribute("failed",     gFailed),
                    new XAttribute("skipped",    gSkipped),
                    new XAttribute("result",     gResult),
                    new XAttribute("start-time", gStart.ToString("yyyy-MM-ddTHH:mm:ss.fffffff")),
                    new XAttribute("duration",   gDuration.ToString("F3")));

                foreach (var r in group)
                {
                    var nunitResult = r.Status switch
                    {
                        TestStatus.Pass  => "Passed",
                        TestStatus.Fail  => "Failed",
                        TestStatus.Error => "Error",
                        TestStatus.Skip  => "Skipped",
                        _                => "Unknown"
                    };

                    var tc = new XElement("test-case",
                        new XAttribute("name",       r.TestName),
                        new XAttribute("fullname",   r.FullName ?? r.TestName),
                        new XAttribute("result",     nunitResult),
                        new XAttribute("start-time", r.StartTime.ToString("yyyy-MM-ddTHH:mm:ss.fffffff")),
                        new XAttribute("duration",   (r.DurationMs / 1000.0).ToString("F3")));

                    if (r.Status is TestStatus.Fail or TestStatus.Error)
                    {
                        tc.Add(new XElement("failure",
                            new XElement("message",     new XCData(r.ExceptionMessage ?? "")),
                            new XElement("stack-trace", new XCData(r.StackTrace ?? ""))));
                    }

                    if (r.Status == TestStatus.Skip)
                        tc.Add(new XElement("reason", new XElement("message", r.SkipReason ?? "")));

                    // Properties: Browser, Environment, RetryCount
                    var props = new XElement("properties");
                    if (!string.IsNullOrWhiteSpace(r.Browser))
                        props.Add(MakeProp("Browser", r.Browser!));
                    if (!string.IsNullOrWhiteSpace(r.Environment))
                        props.Add(MakeProp("Environment", r.Environment!));
                    if (r.RetryCount > 0)
                        props.Add(MakeProp("RetryCount", r.RetryCount.ToString()));
                    if (props.HasElements) tc.Add(props);

                    fixture.Add(tc);
                }

                testRun.Add(fixture);
            }

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            new XDocument(new XDeclaration("1.0", "utf-8", null), testRun)
                .Save(outputPath);
        }

        private static XElement MakeProp(string name, string value)
            => new("property", new XAttribute("name", name), new XAttribute("value", value));
    }
}
