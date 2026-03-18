using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using SimpleSeleniumSupport.Diagnostics;
using SimpleSeleniumSupport.Logging;
using SimpleSeleniumSupport.Network;
using System.Text;

namespace SimpleSeleniumSupport.AI
{
    public static class AISuggestFixExtensions
    {
        private static readonly ILogger _log = LibraryLogger.ForCategory("SimpleSeleniumSupport.AI.AISuggestFix");

        /// <summary>
        /// Analyze failure and ask the AI for suggested fix. Also capture diagnostics to disk.
        /// </summary>
        /// <param name="driver">Current IWebDriver</param>
        /// <param name="ex">Exception thrown by the test</param>
        /// <param name="testName">Optional test name used to name artifacts</param>
        /// <param name="capture">Optional Capture instance for network traces</param>
        /// <param name="consoleLogs">Optional console logs collected during the test</param>
        /// <param name="aiModel">Model name override (null = use provider default)</param>
        /// <returns>AI suggestion text</returns>
        public static string AnalyzeAndSuggestFix(this IWebDriver driver, Exception ex, string? testName = null, Capture? capture = null, IEnumerable<ConsoleLogEntry>? consoleLogs = null, string? aiModel = null)
        {
            aiModel ??= SimpleSeleniumSupportDefaults.OllamaModel;
            testName ??= "UnnamedTest";

            // 1) Save diagnostics first (screenshot, HTML, console, network export)
            string diagFolder = "[not-saved]";
            try
            {
                diagFolder = FailureDiagnostics.SaveDiagnostics(driver, capture, consoleLogs, baseFolder: "Diagnostics");
            }
            catch (Exception diagEx)
            {
                _log.LogWarning(diagEx, "[AISuggestFix] Failed to save diagnostics: {Message}", diagEx.Message);
            }

            // 2) Build a summary prompt for the AI
            var sb = new StringBuilder();
            sb.AppendLine("You are an expert test automation engineer.");
            sb.AppendLine($"Test name: {testName}");
            sb.AppendLine($"Exception: {ex.GetType().FullName}: {ex.Message}");
            sb.AppendLine();
            sb.AppendLine("Environment / Diagnostics:");
            sb.AppendLine($"- Diagnostics folder: {diagFolder}");
            sb.AppendLine($"- Current URL: {SafeGetUrl(driver)}");
            sb.AppendLine($"- Page title: {SafeGetTitle(driver)}");
            sb.AppendLine();
            sb.AppendLine("Task:");
            sb.AppendLine("1) Suggest 2 short possible explanations for why this failure occurred (1-2 lines each).");
            sb.AppendLine("2) Suggest 2 concrete remediations (code-level suggestions) that would likely fix the issue for Selenium C# tests. Include example XPath or CSS selectors if helpful.");
            sb.AppendLine("3) If possible, attempt to identify a robust XPath for the element that likely failed, and return only the XPath on its own line labelled XPATH_CANDIDATE:");
            sb.AppendLine();
            sb.AppendLine("Important constraints:");
            sb.AppendLine("- Prefer selectors that rely on data-testid, id, name, or stable attributes.");
            sb.AppendLine("- Avoid absolute indices or long absolute XPaths like /html/body/... unless no other option exists.");
            sb.AppendLine();
            sb.AppendLine("Please respond in plain text. If you cannot provide a selector, explain why.");
            sb.AppendLine();

            // Add a small snapshot of visible DOM (not full page) to reduce payload & privacy risk
            try
            {
                string domSnippet = "[no-dom]";
                if (AISuggestFixConfig.UseTrimmedDom)
                {
                    domSnippet = DomTrimmer.TrimDom(driver, AISuggestFixConfig.TrimMaxNodes, AISuggestFixConfig.TrimMaxChars);
                    sb.AppendLine("--- VISIBLE DOM SNIPPET (trimmed) ---");
                    if (AISuggestFixConfig.SanitizeBeforeSend)
                        domSnippet = Sanitization.PromptSanitizer.SanitizeDomJson(
                            domSnippet, AISuggestFixConfig.SanitizationOptions);
                    sb.AppendLine(domSnippet);
                    sb.AppendLine();
                }
                else
                {
                    // If trimmed DOM is disabled, add a tiny fallback summary only
                    sb.AppendLine("--- NOTE ---");
                    sb.AppendLine("Trimmed DOM disabled; only including small environment summary.");
                    sb.AppendLine();
                }
            }
            catch
            {
                sb.AppendLine("[dom-trim-failed]");
            }
            try
            {
                var diagFiles = System.IO.Directory.GetFiles(diagFolder, "*", System.IO.SearchOption.TopDirectoryOnly);
                var har = diagFiles.FirstOrDefault(p => p.EndsWith(".har", StringComparison.OrdinalIgnoreCase));
                if (!string.IsNullOrWhiteSpace(har))
                {
                    sb.AppendLine();
                    sb.AppendLine($"A HAR file for network traces was saved at: {har}");
                    sb.AppendLine("Review the HAR for full request/response payloads; do not attempt to fetch external URLs automatically.");
                }
            }
            catch { /* ignore */ }
            var prompt = sb.ToString();
            if (AISuggestFixConfig.SanitizeBeforeSend)
                prompt = Sanitization.PromptSanitizer.Sanitize(
                    prompt, AISuggestFixConfig.SanitizationOptions);

            // 3) Call AI provider safely
            string aiResponse;
            try
            {
                var opts   = new AIRequestOptions { Model = aiModel };
                var result = AIProviderRegistry.Current.Generate(prompt, opts);
                aiResponse = result.Success ? result.Text : $"AI call failed: {result.ErrorMessage}";
            }
            catch (Exception apiEx)
            {
                aiResponse = $"AI call failed: {apiEx.Message}";
            }

            // 4) Try to extract an XPath candidate from the AI response and validate it quickly
            try
            {
                var candidate = ExtractXPathCandidate(aiResponse);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    try
                    {
                        var normalized = AIElementFinder.NormalizeXpath(candidate);
                        // Try to find the element quickly (no exception throw handled)
                        var elems = driver.FindElements(By.XPath(normalized));
                        if (elems.Count == 1)
                        {
                            aiResponse += Environment.NewLine + $"(Validated selector matches 1 element: {normalized})";
                        }
                        else if (elems.Count > 1)
                        {
                            aiResponse += Environment.NewLine + $"(Validated selector matches {elems.Count} elements - may be too broad: {normalized})";
                        }
                        else
                        {
                            aiResponse += Environment.NewLine + $"(AI selector did not match any elements: {normalized})";
                        }
                    }
                    catch (Exception selEx)
                    {
                        aiResponse += Environment.NewLine + $"(Selector validation failed: {selEx.Message})";
                    }
                }
            }
            catch { /* swallow */ }

            // 5) Return AI response
            return aiResponse;
        }

        /// <summary>
        /// Async version of <see cref="AnalyzeAndSuggestFix"/>.
        /// Routes the AI call through <see cref="AIProviderRegistry.Current"/> without blocking.
        /// </summary>
        /// <param name="driver">Current IWebDriver</param>
        /// <param name="ex">Exception thrown by the test</param>
        /// <param name="testName">Optional test name used to name artifacts</param>
        /// <param name="capture">Optional Capture instance for network traces</param>
        /// <param name="consoleLogs">Optional console logs collected during the test</param>
        /// <param name="aiModel">Model name override (null = use provider default)</param>
        /// <param name="cancellationToken">Optional cancellation token</param>
        /// <returns>AI suggestion text</returns>
        public static async Task<string> AnalyzeAndSuggestFixAsync(
            this IWebDriver driver,
            Exception ex,
            string? testName = null,
            Capture? capture = null,
            IEnumerable<ConsoleLogEntry>? consoleLogs = null,
            string? aiModel = null,
            CancellationToken cancellationToken = default)
        {
            testName ??= "UnnamedTest";

            string diagFolder = "[not-saved]";
            try
            {
                diagFolder = FailureDiagnostics.SaveDiagnostics(driver, capture, consoleLogs, baseFolder: "Diagnostics");
            }
            catch (Exception diagEx)
            {
                _log.LogWarning(diagEx, "[AISuggestFix] Failed to save diagnostics: {Message}", diagEx.Message);
            }

            var sb = new StringBuilder();
            sb.AppendLine("You are an expert test automation engineer.");
            sb.AppendLine($"Test name: {testName}");
            sb.AppendLine($"Exception: {ex.GetType().FullName}: {ex.Message}");
            sb.AppendLine();
            sb.AppendLine($"- Diagnostics folder: {diagFolder}");
            sb.AppendLine($"- Current URL: {SafeGetUrl(driver)}");
            sb.AppendLine($"- Page title: {SafeGetTitle(driver)}");
            sb.AppendLine();
            sb.AppendLine("1) Suggest 2 short possible explanations for why this failure occurred.");
            sb.AppendLine("2) Suggest 2 concrete remediations (code-level suggestions) for Selenium C# tests.");
            sb.AppendLine("3) If possible, return a robust XPath on its own line labelled XPATH_CANDIDATE:");

            try
            {
                if (AISuggestFixConfig.UseTrimmedDom)
                {
                    sb.AppendLine();
                    sb.AppendLine("--- VISIBLE DOM SNIPPET (trimmed) ---");
                    var domSnippetAsync = DomTrimmer.TrimDom(
                        driver, AISuggestFixConfig.TrimMaxNodes, AISuggestFixConfig.TrimMaxChars);
                    if (AISuggestFixConfig.SanitizeBeforeSend)
                        domSnippetAsync = Sanitization.PromptSanitizer.SanitizeDomJson(
                            domSnippetAsync, AISuggestFixConfig.SanitizationOptions);
                    sb.AppendLine(domSnippetAsync);
                }
            }
            catch { sb.AppendLine("[dom-trim-failed]"); }

            var asyncPrompt = sb.ToString();
            if (AISuggestFixConfig.SanitizeBeforeSend)
                asyncPrompt = Sanitization.PromptSanitizer.Sanitize(
                    asyncPrompt, AISuggestFixConfig.SanitizationOptions);

            var opts   = new AIRequestOptions { Model = aiModel };
            var result = await AIProviderRegistry.Current
                .GenerateAsync(asyncPrompt, opts, cancellationToken)
                .ConfigureAwait(false);

            var aiResponse = result.Success ? result.Text : $"AI call failed: {result.ErrorMessage}";

            // Validate XPath candidate
            try
            {
                var candidate = ExtractXPathCandidate(aiResponse);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    var normalized = AIElementFinder.NormalizeXpath(candidate);
                    var elems = driver.FindElements(By.XPath(normalized));
                    aiResponse += Environment.NewLine + (elems.Count == 1
                        ? $"(Validated selector matches 1 element: {normalized})"
                        : $"(Validated selector matches {elems.Count} elements: {normalized})");
                }
            }
            catch { /* swallow */ }

            return aiResponse;
        }

        private static string SafeGetUrl(IWebDriver driver)
        {
            try { return driver.Url ?? "[no-url]"; }
            catch { return "[url-access-failed]"; }
        }

        private static string SafeGetTitle(IWebDriver driver)
        {
            try { return driver.Title ?? "[no-title]"; }
            catch { return "[title-access-failed]"; }
        }

        private static string? ExtractXPathCandidate(string aiText)
        {
            if (string.IsNullOrWhiteSpace(aiText)) return null;
            // look for a line starting with XPATH_CANDIDATE: or first occurrence of '//' or '/html'
            var lines = aiText.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var l in lines)
            {
                var t = l.Trim();
                if (t.StartsWith("XPATH_CANDIDATE:", StringComparison.OrdinalIgnoreCase))
                    return t.Substring("XPATH_CANDIDATE:".Length).Trim();

                var idx = t.IndexOf("//", StringComparison.Ordinal);
                if (idx >= 0 && t.Length - idx > 2)
                    return t.Substring(idx).Trim();
            }

            // fallback: require '//' so we don't mistake URLs for XPath expressions
            var doubleSlash = aiText.IndexOf("//", StringComparison.Ordinal);
            if (doubleSlash >= 0)
            {
                var sub = aiText.Substring(doubleSlash)
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                if (sub.Length > 4) return sub;  // "//x" is minimum viable XPath
            }

            return null;
        }

    }
}
