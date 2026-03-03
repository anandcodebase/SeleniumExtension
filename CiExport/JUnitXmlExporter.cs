using SimpleSeleniumSupport.Reporting;
using System.Xml.Linq;

namespace SimpleSeleniumSupport.CiExport
{
    /// <summary>
    /// Exports <see cref="TestResult"/> collections to JUnit 4 XML (Surefire format).
    /// <para>
    /// The output is compatible with Jenkins, GitLab CI, GitHub Actions test reports,
    /// and any tool that reads the de-facto JUnit XML standard.
    /// </para>
    /// </summary>
    public static class JUnitXmlExporter
    {
        /// <summary>
        /// Exports results to a JUnit XML file.
        /// </summary>
        /// <param name="results">Test results to export.</param>
        /// <param name="outputPath">Full path to the output .xml file.</param>
        /// <param name="suiteName">Optional top-level suite name. Defaults to "TestRun".</param>
        public static void Export(
            IEnumerable<TestResult> results,
            string outputPath,
            string suiteName = "TestRun")
        {
            var list = results.ToList();
            var byGroup = list.GroupBy(r => r.TestSuite ?? "Default").ToList();

            var totalSec = list.Sum(r => r.DurationMs) / 1000.0;
            var failures  = list.Count(r => r.Status == TestStatus.Fail);
            var errors    = list.Count(r => r.Status == TestStatus.Error);
            var skipped   = list.Count(r => r.Status == TestStatus.Skip);

            var testSuites = new XElement("testsuites",
                new XAttribute("name",     suiteName),
                new XAttribute("time",     totalSec.ToString("F3")),
                new XAttribute("tests",    list.Count),
                new XAttribute("failures", failures),
                new XAttribute("errors",   errors),
                new XAttribute("skipped",  skipped));

            foreach (var group in byGroup)
            {
                var gSec     = group.Sum(r => r.DurationMs) / 1000.0;
                var gFail    = group.Count(r => r.Status == TestStatus.Fail);
                var gErr     = group.Count(r => r.Status == TestStatus.Error);
                var gSkipped = group.Count(r => r.Status == TestStatus.Skip);

                var suite = new XElement("testsuite",
                    new XAttribute("name",      group.Key),
                    new XAttribute("tests",     group.Count()),
                    new XAttribute("failures",  gFail),
                    new XAttribute("errors",    gErr),
                    new XAttribute("skipped",   gSkipped),
                    new XAttribute("time",      gSec.ToString("F3")),
                    new XAttribute("timestamp", group.Min(r => r.StartTime).ToString("O")));

                foreach (var r in group)
                {
                    var tc = new XElement("testcase",
                        new XAttribute("name",      r.TestName),
                        new XAttribute("classname", r.FullName ?? r.TestSuite ?? r.TestName),
                        new XAttribute("time",      (r.DurationMs / 1000.0).ToString("F3")));

                    switch (r.Status)
                    {
                        case TestStatus.Fail:
                            tc.Add(new XElement("failure",
                                new XAttribute("message", r.ExceptionMessage ?? ""),
                                new XAttribute("type",    r.ExceptionType ?? "AssertionError"),
                                new XCData(r.StackTrace ?? "")));
                            break;

                        case TestStatus.Error:
                            tc.Add(new XElement("error",
                                new XAttribute("message", r.ExceptionMessage ?? ""),
                                new XAttribute("type",    r.ExceptionType ?? "Error"),
                                new XCData(r.StackTrace ?? "")));
                            break;

                        case TestStatus.Skip:
                            tc.Add(new XElement("skipped",
                                new XAttribute("message", r.SkipReason ?? "")));
                            break;
                    }

                    if (!string.IsNullOrWhiteSpace(r.AiAnalysis))
                        tc.Add(new XElement("system-out", new XCData(r.AiAnalysis)));

                    suite.Add(tc);
                }

                testSuites.Add(suite);
            }

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            new XDocument(new XDeclaration("1.0", "utf-8", null), testSuites)
                .Save(outputPath);
        }
    }
}
