using Microsoft.Extensions.Logging;
using SimpleSeleniumSupport.Reporting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SimpleSeleniumSupport.AI
{
    /// <summary>
    /// Analyses failed test results with an AI provider to classify whether each failure
    /// is a <c>TestIssue</c>, <c>ProductIssue</c>, <c>Flaky</c>, <c>Infrastructure</c>,
    /// or <c>Uncertain</c> root cause.
    /// <para>
    /// Failed tests are grouped per <see cref="FailureAnalysisOptions.Grouping"/>, sanitized,
    /// sent to <see cref="AIProviderRegistry.Current"/>, and the structured response is parsed
    /// back into <see cref="FailureAnalysisResult"/> objects.  When
    /// <see cref="FailureAnalysisOptions.MutateTestResults"/> is <see langword="true"/> (default),
    /// results are written back to <see cref="TestResult.AiAnalysis"/>,
    /// <see cref="TestResult.AiClassification"/>, and <see cref="TestResult.AiConfidence"/>
    /// so they appear automatically in any subsequent report export.
    /// </para>
    /// </summary>
    /// <example>
    /// // In NUnit [OneTimeTearDown]:
    /// await _collector.AnalyzeFailuresAsync(new FailureAnalysisOptions {
    ///     Grouping = FailureGroupingStrategy.PerCategory });
    /// _collector.ExportSingleFile("TestReports", "Suite Name");
    /// </example>
    public sealed class TestFailureAnalyzer
    {
        private readonly FailureAnalysisOptions _opts;
        private readonly ILogger _log;

        /// <summary>
        /// Creates a new analyzer with optional configuration.
        /// When <paramref name="options"/> is <see langword="null"/>, all defaults are read from
        /// <see cref="SimpleSeleniumSupportDefaults"/>.
        /// </summary>
        public TestFailureAnalyzer(FailureAnalysisOptions? options = null)
        {
            _opts = options ?? new FailureAnalysisOptions();
            _log  = Logging.LibraryLogger.ForCategory("SimpleSeleniumSupport.AI.TestFailureAnalyzer");
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Synchronous wrapper for <see cref="AnalyzeAsync"/>.
        /// Blocks the calling thread until all AI calls complete.
        /// </summary>
        public IReadOnlyList<FailureAnalysisResult> Analyze(IEnumerable<TestResult> results)
            => AnalyzeAsync(results, CancellationToken.None).GetAwaiter().GetResult();

        /// <summary>
        /// Filters <paramref name="results"/> to <see cref="TestStatus.Fail"/> and
        /// <see cref="TestStatus.Error"/>, groups them per
        /// <see cref="FailureAnalysisOptions.Grouping"/>, calls the AI provider for each group,
        /// parses the structured response, and returns one
        /// <see cref="FailureAnalysisResult"/> per failure.
        /// </summary>
        public async Task<IReadOnlyList<FailureAnalysisResult>> AnalyzeAsync(
            IEnumerable<TestResult> results,
            CancellationToken ct = default)
        {
            var failures = (results ?? Enumerable.Empty<TestResult>())
                .Where(r => r.Status == TestStatus.Fail || r.Status == TestStatus.Error)
                .ToList();

            if (failures.Count == 0)
                return Array.Empty<FailureAnalysisResult>();

            // Group and analyse with bounded parallelism
            var groups = failures
                .GroupBy(r => GetGroupKey(r, _opts.Grouping))
                .ToDictionary(g => g.Key, g => (IReadOnlyList<TestResult>)g.ToList());

            using var semaphore = new SemaphoreSlim(_opts.MaxParallelism, _opts.MaxParallelism);
            var tasks = groups
                .Select(kv => AnalyzeGroupAsync(kv.Key, kv.Value, semaphore, ct))
                .ToList();

            var groupResults = await Task.WhenAll(tasks).ConfigureAwait(false);
            var allResults   = groupResults.SelectMany(r => r).ToList();

            // Optionally write classification back onto the source TestResult objects
            if (_opts.MutateTestResults)
            {
                var byName = allResults.ToDictionary(
                    r => r.TestName, r => r, StringComparer.OrdinalIgnoreCase);

                foreach (var tr in failures)
                {
                    if (byName.TryGetValue(tr.TestName, out var ar))
                    {
                        tr.AiAnalysis       = ar.ToMarkdown();
                        tr.AiClassification = ar.Classification.ToString();
                        tr.AiConfidence     = ar.Confidence.ToString();
                    }
                }
            }

            return allResults;
        }

        // ── Group analysis ────────────────────────────────────────────────────

        private async Task<IReadOnlyList<FailureAnalysisResult>> AnalyzeGroupAsync(
            string groupKey,
            IReadOnlyList<TestResult> group,
            SemaphoreSlim semaphore,
            CancellationToken ct)
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var prompt = BuildGroupPrompt(groupKey, group);
                _log.LogDebug(
                    "[FailureAnalyzer] Analyzing group '{Key}' ({Count} test(s), {Chars} prompt chars)",
                    groupKey, group.Count, prompt.Length);

                var opts   = new AIRequestOptions { Model = _opts.AiModel };
                var result = await AIProviderRegistry.Current
                    .GenerateAsync(prompt, opts, ct)
                    .ConfigureAwait(false);

                if (!result.Success)
                {
                    _log.LogWarning(
                        "[FailureAnalyzer] AI call failed for group '{Key}': {Error}",
                        groupKey, result.ErrorMessage);
                    return group
                        .Select(r => ErrorResult(r.TestName, groupKey,
                            $"AI call failed: {result.ErrorMessage}"))
                        .ToList();
                }

                _log.LogDebug(
                    "[FailureAnalyzer] Group '{Key}' response: {Chars} chars, {Ms}ms",
                    groupKey, result.Text.Length, result.LatencyMs);

                return ParseGroupResponse(result.Text, group, groupKey);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _log.LogWarning(ex,
                    "[FailureAnalyzer] Exception analyzing group '{Key}': {Message}",
                    groupKey, ex.Message);
                return group
                    .Select(r => ErrorResult(r.TestName, groupKey, ex.Message))
                    .ToList();
            }
            finally
            {
                semaphore.Release();
            }
        }

        // ── Prompt construction ───────────────────────────────────────────────

        private string BuildGroupPrompt(string groupKey, IReadOnlyList<TestResult> group)
        {
            var sb = new StringBuilder();
            sb.AppendLine("You are an expert test automation engineer.");
            sb.AppendLine("Analyze the following test failure(s) and determine the root cause category for EACH test.");
            sb.AppendLine();
            sb.AppendLine("ROOT CAUSE CATEGORIES:");
            sb.AppendLine("  TEST_ISSUE       - Bug in test code, selector, assertion logic, or test data");
            sb.AppendLine("  PRODUCT_ISSUE    - Genuine defect in the application under test");
            sb.AppendLine("  FLAKY            - Intermittent failure: timing, race conditions, retry exhaustion (check RetryCount > 0)");
            sb.AppendLine("  INFRASTRUCTURE   - CI/CD, browser driver version, network, or missing dependency");
            sb.AppendLine("  UNCERTAIN        - Insufficient information to determine root cause");
            sb.AppendLine();
            sb.AppendLine($"=== GROUP: {groupKey} | {group.Count} failure(s) ===");
            sb.AppendLine();

            // Distribute the prompt budget evenly across tests in the group
            int footerReserve = 400 + group.Count * 120;   // format template per test
            int budget        = _opts.MaxGroupPromptChars - sb.Length - footerReserve;
            int perTest       = group.Count > 0 ? Math.Max(300, budget / group.Count) : 300;

            for (int i = 0; i < group.Count; i++)
            {
                var section = BuildTestSection(i + 1, group.Count, group[i], perTest);
                if (sb.Length + section.Length + footerReserve > _opts.MaxGroupPromptChars)
                {
                    int remaining = group.Count - i;
                    sb.AppendLine($"[{remaining} more failure(s) omitted — prompt budget exceeded. Classify omitted tests as UNCERTAIN]");
                    break;
                }
                sb.Append(section);
            }

            // Required response format instructions
            sb.AppendLine("=== REQUIRED RESPONSE FORMAT ===");
            sb.AppendLine("For EACH test listed above, output one block in EXACTLY this format.");
            sb.AppendLine("Separate blocks with a line containing only three dashes: ---");
            sb.AppendLine("Do NOT write any text outside the blocks.");
            sb.AppendLine();
            foreach (var r in group)
            {
                sb.AppendLine($"TEST: {r.TestName}");
                sb.AppendLine("CLASSIFICATION: TEST_ISSUE | PRODUCT_ISSUE | FLAKY | INFRASTRUCTURE | UNCERTAIN");
                sb.AppendLine("CONFIDENCE: HIGH | MEDIUM | LOW");
                sb.AppendLine("REASONING: One paragraph explaining the root cause evidence.");
                sb.AppendLine("SUGGESTED_FIX: One concrete actionable sentence.");
                sb.AppendLine("---");
            }

            return sb.ToString();
        }

        private string BuildTestSection(int num, int total, TestResult r, int charBudget)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"--- TEST {num} of {total}: {r.TestName} ---");

            if (!string.IsNullOrEmpty(r.TestSuite))  sb.AppendLine($"Class:    {r.TestSuite}");
            if (!string.IsNullOrEmpty(r.Category))   sb.AppendLine($"Category: {r.Category}");
            if (!string.IsNullOrEmpty(r.Browser))    sb.AppendLine($"Browser:  {r.Browser}");

            sb.AppendLine($"Duration: {r.DurationMs:F0}ms   RetryCount: {r.RetryCount}" +
                          $"   FlakinessScore: {r.FlakinessScore?.ToString("P0") ?? "N/A"}");

            if (!string.IsNullOrEmpty(r.ExceptionType))
                sb.AppendLine($"ExceptionType: {r.ExceptionType}");

            if (!string.IsNullOrEmpty(r.ExceptionMessage))
            {
                var msg = Sanitize(r.ExceptionMessage);
                if (msg.Length > 500) msg = msg.Substring(0, 500) + "…";
                sb.AppendLine($"Message: {msg}");
            }

            if (!string.IsNullOrEmpty(r.StackTrace))
            {
                var st = Sanitize(r.StackTrace);
                if (_opts.TrimSystemFrames)
                    st = TrimSystemFrames(st);
                if (st.Length > _opts.MaxStackTraceCharsPerTest)
                    st = st.Substring(0, _opts.MaxStackTraceCharsPerTest) + "\n[stack trace truncated]";
                if (!string.IsNullOrWhiteSpace(st))
                {
                    sb.AppendLine("StackTrace:");
                    sb.AppendLine(st);
                }
            }

            if (_opts.IncludeScreenshot)
            {
                if (!string.IsNullOrEmpty(r.ScreenshotPath))
                {
                    // When path redaction is on, expose only the filename
                    var ss = _opts.Sanitization?.RedactFilePaths == true
                        ? System.IO.Path.GetFileName(r.ScreenshotPath)
                        : r.ScreenshotPath;
                    sb.AppendLine($"Screenshot: {ss}");
                }
                else
                {
                    sb.AppendLine("Screenshot: [none]");
                }
            }

            sb.AppendLine();
            return sb.ToString();
        }

        // ── Response parsing ──────────────────────────────────────────────────

        private IReadOnlyList<FailureAnalysisResult> ParseGroupResponse(
            string rawResponse,
            IReadOnlyList<TestResult> group,
            string groupKey)
        {
            var parsed = new Dictionary<string, FailureAnalysisResult>(StringComparer.OrdinalIgnoreCase);

            // Split on separator lines ("---" possibly surrounded by whitespace)
            var blocks = Regex.Split(rawResponse, @"^\s*-{3,}\s*$", RegexOptions.Multiline)
                .Select(b => b.Trim())
                .Where(b => b.Length > 0)
                .ToList();

            foreach (var block in blocks)
            {
                var ar = ParseBlock(block, groupKey);
                if (!string.IsNullOrEmpty(ar.TestName))
                    parsed[ar.TestName] = ar;
            }

            // Guarantee one result per test, filling in Uncertain for any missed tests
            return group.Select(r =>
            {
                if (parsed.TryGetValue(r.TestName, out var found)) return found;
                return ErrorResult(r.TestName, groupKey, "[AI did not return analysis for this test]");
            }).ToList();
        }

        private static FailureAnalysisResult ParseBlock(string block, string groupKey)
        {
            var result = new FailureAnalysisResult { GroupKey = groupKey, RawResponse = block };

            // Single-line field extractor
            string? Line(string label)
            {
                var m = Regex.Match(block,
                    $@"^{Regex.Escape(label)}\s*:?\s*(.+)$",
                    RegexOptions.Multiline | RegexOptions.IgnoreCase);
                return m.Success ? m.Groups[1].Value.Trim() : null;
            }

            // Multi-line field extractor (up to the next labelled field)
            string? MultiLine(string label)
            {
                var m = Regex.Match(block,
                    $@"^{Regex.Escape(label)}\s*:?\s*([\s\S]+?)(?=\n(?:TEST|CLASSIFICATION|CONFIDENCE|REASONING|SUGGESTED_?FIX)\s*:|$)",
                    RegexOptions.Multiline | RegexOptions.IgnoreCase);
                return m.Success ? m.Groups[1].Value.Trim() : null;
            }

            result.TestName     = Line("TEST") ?? "";
            result.Reasoning    = MultiLine("REASONING") ?? Line("REASONING") ?? "";
            result.SuggestedFix = Line("SUGGESTED_FIX") ?? Line("SUGGESTED FIX") ?? MultiLine("SUGGESTED_FIX") ?? "";

            var classStr = Line("CLASSIFICATION") ?? "";
            var confStr  = Line("CONFIDENCE")     ?? "";

            result.Classification = ParseClassification(classStr, block);
            result.Confidence     = ParseConfidence(confStr);

            return result;
        }

        private static FailureClassification ParseClassification(string value, string fullBlock)
        {
            // Normalize: strip spaces and underscores, upper-case
            string Norm(string s) => s.ToUpperInvariant().Replace("_", "").Replace(" ", "");

            var v = Norm(value);
            if (v.Contains("PRODUCTISSUE") || v.Contains("PRODUCTBUG") || v == "PRODUCT")
                return FailureClassification.ProductIssue;
            if (v.Contains("TESTISSUE") || v.Contains("TESTCODE") || v.Contains("TESTBUG") || v == "TEST")
                return FailureClassification.TestIssue;
            if (v.Contains("FLAKY") || v.Contains("INTERMITTENT"))
                return FailureClassification.Flaky;
            if (v.Contains("INFRASTRUCTURE") || v.Contains("INFRA") || v.Contains("CICD") || v.Contains("ENV"))
                return FailureClassification.Infrastructure;

            // Fallback: scan the full block for classification keywords
            var blockNorm = Norm(fullBlock);
            if (blockNorm.Contains("PRODUCTISSUE") || blockNorm.Contains("PRODUCT_ISSUE"))
                return FailureClassification.ProductIssue;
            if (blockNorm.Contains("TESTISSUE") || blockNorm.Contains("TEST_ISSUE"))
                return FailureClassification.TestIssue;
            if (blockNorm.Contains("FLAKY"))
                return FailureClassification.Flaky;
            if (blockNorm.Contains("INFRASTRUCTURE"))
                return FailureClassification.Infrastructure;

            return FailureClassification.Uncertain;
        }

        private static AnalysisConfidence ParseConfidence(string value)
        {
            var v = value.ToUpperInvariant();
            if (v.Contains("HIGH")) return AnalysisConfidence.High;
            if (v.Contains("MED"))  return AnalysisConfidence.Medium;
            return AnalysisConfidence.Low;
        }

        // ── Grouping key ──────────────────────────────────────────────────────

        private static string GetGroupKey(TestResult r, FailureGroupingStrategy strategy) =>
            strategy switch
            {
                FailureGroupingStrategy.PerTest     => r.TestName,
                FailureGroupingStrategy.PerClass    => r.TestSuite    ?? "(no class)",
                FailureGroupingStrategy.PerCategory => r.Category     ?? "(no category)",
                FailureGroupingStrategy.PerBrowser  => r.Browser      ?? "(no browser)",
                FailureGroupingStrategy.PerTag      => FirstTag(r.Tags),
                FailureGroupingStrategy.AllTogether => "All Failures",
                _                                   => r.TestSuite    ?? "(no class)"
            };

        private static string FirstTag(string? tags)
        {
            if (string.IsNullOrWhiteSpace(tags)) return "(no tag)";
            var first = tags.Split(',')[0].Trim();
            return string.IsNullOrEmpty(first) ? "(no tag)" : first;
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        // Removes stack-trace lines that belong to framework internals, keeping only the
        // user/library frames where the actual bug lives.
        private static readonly Regex _systemFrameRegex = new(
            @"^\s+at\s+(System\.|Microsoft\.|NUnit\.|Xunit\.|MSTest\.|mscorlib\.|netstandard\." +
            @"|System\.Runtime\.|System\.Reflection\.|System\.Threading\.|System\.Private\.)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Multiline);

        private static string TrimSystemFrames(string stackTrace)
        {
            var lines  = stackTrace.Split('\n');
            var kept   = lines.Where(l => !_systemFrameRegex.IsMatch(l)).ToArray();
            // If trimming removed everything, fall back to original so we don't send blank context.
            return kept.All(string.IsNullOrWhiteSpace) ? stackTrace : string.Join('\n', kept);
        }

        private string Sanitize(string text)
        {
            if (_opts.Sanitization == null) return text;
            return Sanitization.PromptSanitizer.Sanitize(text, _opts.Sanitization);
        }

        private static FailureAnalysisResult ErrorResult(
            string testName, string groupKey, string reason) =>
            new FailureAnalysisResult
            {
                TestName       = testName,
                Classification = FailureClassification.Uncertain,
                Confidence     = AnalysisConfidence.Low,
                Reasoning      = reason,
                SuggestedFix   = "Review the failure manually.",
                GroupKey       = groupKey,
                RawResponse    = reason
            };
    }
}
