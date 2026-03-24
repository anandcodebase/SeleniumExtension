using System.Text;

namespace SimpleSeleniumSupport.AI
{
    /// <summary>
    /// Structured AI analysis result for a single failed test,
    /// produced by <see cref="TestFailureAnalyzer"/>.
    /// </summary>
    public sealed class FailureAnalysisResult
    {
        /// <summary>
        /// Name of the test this result belongs to
        /// (matches <see cref="Reporting.TestResult.TestName"/>).
        /// </summary>
        public string TestName { get; set; } = "";

        /// <summary>AI-determined root cause classification.</summary>
        public FailureClassification Classification { get; set; } = FailureClassification.Uncertain;

        /// <summary>AI confidence level in <see cref="Classification"/>.</summary>
        public AnalysisConfidence Confidence { get; set; } = AnalysisConfidence.Low;

        /// <summary>AI explanation of why it reached this classification.</summary>
        public string Reasoning { get; set; } = "";

        /// <summary>Concrete actionable suggestion for fixing the failure.</summary>
        public string SuggestedFix { get; set; } = "";

        /// <summary>Full raw AI response text before structured parsing.</summary>
        public string RawResponse { get; set; } = "";

        /// <summary>
        /// Group key used when this test was analysed as part of a batch
        /// (e.g. class name, category, or "All Failures").
        /// </summary>
        public string? GroupKey { get; set; }

        /// <summary>
        /// Renders the analysis as Markdown suitable for storing in
        /// <see cref="Reporting.TestResult.AiAnalysis"/>.
        /// </summary>
        public string ToMarkdown()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"**Classification:** {Classification} ({Confidence} confidence)");
            if (!string.IsNullOrWhiteSpace(Reasoning))
            {
                sb.AppendLine();
                sb.AppendLine($"**Reasoning:** {Reasoning}");
            }
            if (!string.IsNullOrWhiteSpace(SuggestedFix))
            {
                sb.AppendLine();
                sb.AppendLine($"**Suggested Fix:** {SuggestedFix}");
            }
            return sb.ToString().TrimEnd();
        }
    }
}
