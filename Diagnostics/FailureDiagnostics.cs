using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using SimpleSeleniumSupport.Logging;
using SimpleSeleniumSupport.Network;
using SimpleSeleniumSupport.Reporting;

namespace SimpleSeleniumSupport.Diagnostics
{
    /// <summary>
    /// Failure Diagnostics
    /// </summary>
    public static class FailureDiagnostics
    {
        private static readonly ILogger _log = LibraryLogger.ForCategory("SimpleSeleniumSupport.Diagnostics.FailureDiagnostics");

        /// <summary>
        /// Save diagnostics into a timestamped directory and return the folder path.
        /// Includes screenshot, page HTML, console logs, network export (Excel) and HAR.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="capture">The capture.</param>
        /// <param name="consoleLogs">The console logs.</param>
        /// <param name="baseFolder">The base folder.</param>
        /// <param name="failureMessage">Optional failure/exception message used to annotate the screenshot banner.</param>
        /// <param name="testName">Optional test name shown in the screenshot bottom bar.</param>
        /// <returns>Absolute path of the diagnostics folder.</returns>
        public static string SaveDiagnostics(
            IWebDriver driver,
            Capture? capture = null,
            IEnumerable<ConsoleLogEntry>? consoleLogs = null,
            string baseFolder = "Diagnostics",
            string? failureMessage = null,
            string? testName = null)
        {
            var ts = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
            var folder = Path.Combine(baseFolder, $"failure_{ts}");
            Directory.CreateDirectory(folder);

            try
            {

                Directory.CreateDirectory(folder);

                try
                {
                    // Screenshot (if supported) — save bytes directly to avoid ScreenshotImageFormat issues
                    try
                    {
                        if (driver is ITakesScreenshot snap)
                        {
                            var rawBytes = snap.GetScreenshot().AsByteArray;

                            if (SimpleSeleniumSupportDefaults.AnnotateScreenshotsOnFailure &&
                                !string.IsNullOrEmpty(failureMessage))
                            {
                                // Save un-annotated original alongside the annotated version
                                File.WriteAllBytes(Path.Combine(folder, "screenshot_raw.png"), rawBytes);
                                var annotated = ScreenshotAnnotator.AnnotateFailure(rawBytes, failureMessage, testName);
                                File.WriteAllBytes(Path.Combine(folder, "screenshot.png"), annotated);
                                _log.LogInformation("Saved annotated screenshot: {Folder}", folder);
                            }
                            else
                            {
                                File.WriteAllBytes(Path.Combine(folder, "screenshot.png"), rawBytes);
                                _log.LogInformation("Saved screenshot: {Folder}", folder);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "Screenshot failed: {Message}", ex.Message);
                    }

                    // Page HTML
                    try
                    {
                        var html = driver.PageSource ?? "";
                        var htmlPath = Path.Combine(folder, "page.html");
                        File.WriteAllText(htmlPath, html);
                        _log.LogInformation("Saved page HTML: {Path}", htmlPath);
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "Saving HTML failed: {Message}", ex.Message);
                    }

                    // Console logs
                    try
                    {
                        if (consoleLogs != null)
                        {
                            var clPath = Path.Combine(folder, "console_logs.txt");
                            using var sw = new StreamWriter(clPath);
                            foreach (var e in consoleLogs)
                            {
                                sw.WriteLine($"{e.Timestamp:O} [{e.Level}] {e.Source} {e.LineNumber} {e.Url}");
                                sw.WriteLine(e.Text);
                                sw.WriteLine("----");
                            }
                            _log.LogInformation("Saved console logs: {Path}", clPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "Saving console logs failed: {Message}", ex.Message);
                    }

                    // Network capture to Excel (if provided)
                    try
                    {
                        if (capture != null)
                        {
                            var network = capture.GetCombinedNetworkInfo();
                            if (network != null && network.Any())
                            {
                                var excelPath = Path.Combine(folder, "network_info.xlsx");
                                ExcelExporter.ExportNetworkInfoToExcel(network, excelPath);
                                _log.LogInformation("Exported network info to Excel: {Path}", excelPath);

                                // Also export HAR
                                try
                                {
                                    var harPath = Path.Combine(folder, "network_info.har");
                                    HarExporter.ExportToHar(network, harPath);
                                    _log.LogInformation("Exported HAR: {Path}", harPath);
                                }
                                catch (Exception harEx)
                                {
                                    _log.LogWarning(harEx, "HAR export failed: {Message}", harEx.Message);
                                }
                            }
                        }
                    }

                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "Exporting network info failed: {Message}", ex.Message);
                    }
                }
                catch (Exception ex)
                {
                    _log.LogError(ex, "Diagnostics saving encountered an error: {Message}", ex.Message);
                }

                return folder;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Diagnostics saving encountered an error: {Message}", ex.Message);
            }
            return folder;
        }
    }
}

