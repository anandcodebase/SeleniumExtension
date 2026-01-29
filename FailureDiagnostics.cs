using OpenQA.Selenium;
using SimpleSeleniumSupport.Reporting;

namespace SimpleSeleniumSupport
{
    /// <summary>
    /// Failure Diagnostics
    /// </summary>
    public static class FailureDiagnostics
    {
        /// <summary>
        /// Save diagnostics into a timestamped directory and return the folder path.
        /// Includes screenshot, page HTML, console logs, network export (Excel) and HAR.
        /// </summary>
        /// <param name="driver">The driver.</param>
        /// <param name="capture">The capture.</param>
        /// <param name="consoleLogs">The console logs.</param>
        /// <param name="baseFolder">The base folder.</param>
        /// <returns></returns>
        public static string SaveDiagnostics(IWebDriver driver, Capture capture = null, IEnumerable<ConsoleLogEntry> consoleLogs = null, string baseFolder = "Diagnostics")
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
                            var shot = snap.GetScreenshot();
                            var path = Path.Combine(folder, "screenshot.png");
                            // Save raw bytes (works across Selenium versions)
                            File.WriteAllBytes(path, shot.AsByteArray);
                            Console.WriteLine($"Saved screenshot: {path}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Screenshot failed: {ex.Message}");
                    }

                    // Page HTML
                    try
                    {
                        var html = driver.PageSource ?? "";
                        var htmlPath = Path.Combine(folder, "page.html");
                        File.WriteAllText(htmlPath, html);
                        Console.WriteLine($"Saved page HTML: {htmlPath}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Saving HTML failed: {ex.Message}");
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
                            Console.WriteLine($"Saved console logs: {clPath}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Saving console logs failed: {ex.Message}");
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
                                Console.WriteLine($"Exported network info to Excel: {excelPath}");

                                // Also export HAR
                                try
                                {
                                    var harPath = Path.Combine(folder, "network_info.har");
                                    HarExporter.ExportToHar(network, harPath);
                                    Console.WriteLine($"Exported HAR: {harPath}");
                                }
                                catch (Exception harEx)
                                {
                                    Console.WriteLine($"HAR export failed: {harEx.Message}");
                                }
                            }
                        }
                    }

                    catch (Exception ex)
                    {
                        Console.WriteLine($"Exporting network info failed: {ex.Message}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Diagnostics saving encountered an error: {ex.Message}");
                }

                return folder;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Diagnostics saving encountered an error: {ex.Message}");
            }
            return folder;
        }
    }
}

