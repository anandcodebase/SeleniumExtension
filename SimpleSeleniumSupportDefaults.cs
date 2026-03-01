namespace SimpleSeleniumSupport
{
    /// <summary>
    /// Global default configuration for SimpleSeleniumSupport.
    /// Set these once at test-suite startup (e.g. in a [OneTimeSetUp] or fixture constructor)
    /// to control behaviour across the entire library without touching individual call sites.
    /// </summary>
    /// <example>
    /// // NUnit [OneTimeSetUp] example:
    /// SimpleSeleniumSupportDefaults.OllamaModel      = "llama3.2";
    /// SimpleSeleniumSupportDefaults.OllamaBaseUrl    = "http://gpu-box:11434/api/generate";
    /// SimpleSeleniumSupportDefaults.OllamaTimeoutMs  = 60_000;
    /// SimpleSeleniumSupportDefaults.AiRetries        = 3;
    /// SimpleSeleniumSupportDefaults.WaitTimeoutSeconds = 20;
    /// SimpleSeleniumSupportDefaults.VisualThreshold  = 98.0;
    /// </example>
    public static class SimpleSeleniumSupportDefaults
    {
        // ─── Timeouts ────────────────────────────────────────────────────────────

        /// <summary>
        /// Default timeout (seconds) for AI-powered wait operations
        /// (<c>WaitAndFindByAI</c>, <c>WaitAndFindByAIAsync</c>).
        /// Also seeds <see cref="AI.AIElementFinder.DefaultWaitTimeoutSeconds"/>.
        /// </summary>
        public static int WaitTimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// Default timeout (seconds) for locator wait helpers
        /// (<c>GetByRole</c>, <c>GetByText</c>, <c>WaitUntilVisible</c>, etc.).
        /// New <see cref="Selectors.LocatorOptions"/> instances read this value.
        /// </summary>
        public static int LocatorTimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// Default timeout (seconds) for <c>WaitForRequest</c> and <c>WaitForAllRequests</c>.
        /// </summary>
        public static int NetworkWaitTimeoutSeconds { get; set; } = 30;

        // ─── AI / Ollama ──────────────────────────────────────────────────────────

        /// <summary>
        /// Number of retry attempts when an AI-generated selector fails to locate an element.
        /// Seeds <see cref="AI.AIElementFinder.DefaultRetries"/>.
        /// </summary>
        public static int AiRetries { get; set; } = 2;

        /// <summary>
        /// HTTP timeout (milliseconds) for each Ollama API call.
        /// Seeds <see cref="AI.OllamaClient.TimeoutMs"/>.
        /// </summary>
        public static int OllamaTimeoutMs { get; set; } = 30_000;

        /// <summary>
        /// Ollama model name used by all AI methods when no explicit model is supplied.
        /// Seeds <see cref="AI.OllamaClient"/> and all <c>ollamaModel</c> parameters
        /// whose callers omit the argument.
        /// </summary>
        public static string OllamaModel { get; set; } = "llama3";

        /// <summary>
        /// Full URL to the Ollama /api/generate endpoint.
        /// Seeds <see cref="AI.OllamaClient.ApiUrl"/>.
        /// </summary>
        public static string OllamaBaseUrl { get; set; } = "http://localhost:11434/api/generate";

        // ─── Visual regression ────────────────────────────────────────────────────

        /// <summary>
        /// Pass/fail similarity threshold (0–100 %) used by visual comparisons.
        /// A comparison passes when <c>SimilarityPercent &gt;= VisualThreshold</c>.
        /// Seeds <see cref="Image.ImageComparisonOptions.Threshold"/>.
        /// </summary>
        public static double VisualThreshold { get; set; } = 95.0;

        // ─── Video recording ──────────────────────────────────────────────────────

        /// <summary>
        /// Path to the FFmpeg executable used by <see cref="Recording.LocalVideoRecorder"/>.
        /// Defaults to <c>"ffmpeg"</c> which relies on FFmpeg being on the system <c>PATH</c>.
        /// Override with a full path if FFmpeg is not on PATH (e.g. <c>@"C:\tools\ffmpeg\bin\ffmpeg.exe"</c>).
        /// </summary>
        public static string VideoRecordingFfmpegPath { get; set; } = "ffmpeg";

        /// <summary>
        /// Default output directory for recorded video files.
        /// Used by <see cref="Recording.VideoRecordingOptions.OutputDirectory"/>
        /// when no explicit directory is provided.
        /// </summary>
        public static string VideoRecordingOutputDirectory { get; set; } = "Recordings";

        /// <summary>
        /// Default frame rate (frames per second) for local screenshot-based recording.
        /// <para>
        /// Each WebDriver screenshot takes ~100–300 ms, so values above 10 rarely produce
        /// smoother video and may cause frame drops on slow machines. 5 fps is reliable in CI.
        /// </para>
        /// Seeds <see cref="Recording.VideoRecordingOptions.FrameRate"/>.
        /// </summary>
        public static int VideoRecordingFrameRate { get; set; } = 5;
    }
}
