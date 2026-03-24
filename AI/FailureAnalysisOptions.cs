namespace SimpleSeleniumSupport.AI
{
    /// <summary>
    /// Configuration for <see cref="TestFailureAnalyzer"/>.
    /// All numeric properties read their default from <see cref="SimpleSeleniumSupportDefaults"/>
    /// at construction time so that a single global override is sufficient.
    /// </summary>
    public sealed class FailureAnalysisOptions
    {
        /// <summary>
        /// How failed tests are batched before sending to the AI.
        /// Default: <see cref="FailureGroupingStrategy.PerClass"/>.
        /// </summary>
        public FailureGroupingStrategy Grouping { get; set; } =
            SimpleSeleniumSupportDefaults.FailureAnalysisGrouping;

        /// <summary>
        /// AI model name override.
        /// <see langword="null"/> = use the configured provider's default model
        /// (or <see cref="SimpleSeleniumSupportDefaults.OllamaModel"/> for Ollama).
        /// For vision-based screenshot analysis set a vision-capable model such as
        /// <c>"llava"</c> or <c>"gpt-4o"</c>.
        /// </summary>
        public string? AiModel { get; set; }

        /// <summary>
        /// Include the screenshot file path as a reference line in the AI prompt.
        /// For vision models combined with <see cref="UseVisionModel"/>, screenshot bytes
        /// are also embedded as base64.
        /// Default: <c>true</c>.
        /// </summary>
        public bool IncludeScreenshot { get; set; } =
            SimpleSeleniumSupportDefaults.FailureAnalysisIncludeScreenshot;

        /// <summary>
        /// When <see langword="true"/>, screenshot bytes are read from disk and embedded
        /// as base64 in the AI prompt.  Requires a vision-capable model
        /// (<c>"llava"</c>, <c>"gpt-4o"</c>, etc.).
        /// Default: <c>false</c>.
        /// </summary>
        public bool UseVisionModel { get; set; } = false;

        /// <summary>
        /// Maximum characters of exception message + stack trace included per test inside
        /// a group prompt.  Larger values give the AI more context but cost more tokens.
        /// Default: <c>1500</c>.
        /// </summary>
        public int MaxStackTraceCharsPerTest { get; set; } =
            SimpleSeleniumSupportDefaults.FailureAnalysisMaxStackTraceChars;

        /// <summary>
        /// Maximum total characters for the entire group prompt.
        /// Tests whose context would exceed this budget are summarised as
        /// "[N more failures omitted — classify as UNCERTAIN]".
        /// Default: <c>8000</c>.
        /// </summary>
        public int MaxGroupPromptChars { get; set; } =
            SimpleSeleniumSupportDefaults.FailureAnalysisMaxGroupPromptChars;

        /// <summary>
        /// Maximum number of AI calls that run concurrently across groups.
        /// Default: <c>3</c>.
        /// </summary>
        public int MaxParallelism { get; set; } =
            SimpleSeleniumSupportDefaults.FailureAnalysisMaxParallelism;

        /// <summary>
        /// Sanitization rules applied to all prompt text before it is sent to the AI provider.
        /// Defaults to a new <see cref="Sanitization.SanitizationOptions"/> with all redactions enabled.
        /// Set to <see langword="null"/> to disable sanitization entirely.
        /// </summary>
        public Sanitization.SanitizationOptions? Sanitization { get; set; } = new();

        /// <summary>
        /// When <see langword="true"/> (default), analysis results are written back to
        /// <see cref="Reporting.TestResult.AiAnalysis"/>,
        /// <see cref="Reporting.TestResult.AiClassification"/>, and
        /// <see cref="Reporting.TestResult.AiConfidence"/> on each input object
        /// so they appear in any subsequent HTML/Excel report export.
        /// Set to <see langword="false"/> to consume only the returned
        /// <see cref="FailureAnalysisResult"/> list without modifying the source objects.
        /// </summary>
        public bool MutateTestResults { get; set; } = true;
    }
}
