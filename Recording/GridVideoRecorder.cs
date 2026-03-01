using OpenQA.Selenium;
using OpenQA.Selenium.Remote;

namespace SimpleSeleniumSupport.Recording
{
    /// <summary>
    /// Manages server-side video recording on Selenium Grid 4.
    /// <para>
    /// Usage pattern:
    /// <list type="number">
    ///   <item><description>
    ///     Call <see cref="EnableRecording"/> on <c>ChromeOptions</c> / <c>FirefoxOptions</c>
    ///     <b>before</b> creating the <c>RemoteWebDriver</c>.
    ///   </description></item>
    ///   <item><description>
    ///     After the driver is created, instantiate <see cref="GridVideoRecorder"/> (captures the session ID).
    ///   </description></item>
    ///   <item><description>
    ///     Run the test, then call <c>driver.Quit()</c> (Grid finalises the video on session end).
    ///   </description></item>
    ///   <item><description>
    ///     Call <see cref="DownloadVideoAsync"/> to retrieve the MP4 from the Grid hub.
    ///   </description></item>
    /// </list>
    /// </para>
    /// </summary>
    public sealed class GridVideoRecorder
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        private readonly string _sessionId;
        private readonly string _gridHubUrl;
        private readonly string _outputDirectory;

        /// <summary>
        /// Full path to the downloaded video file, or <see langword="null"/> if
        /// <see cref="DownloadVideoAsync"/> has not yet completed successfully.
        /// </summary>
        public string? VideoPath { get; private set; }

        // ── Static helper ─────────────────────────────────────────────────────

        /// <summary>
        /// Adds the Grid 4 capabilities required to enable server-side video recording.
        /// Call this on your <c>DriverOptions</c> object <b>before</b> creating the
        /// <c>RemoteWebDriver</c>.
        /// </summary>
        /// <param name="options">The Selenium driver options (e.g. <c>ChromeOptions</c>).</param>
        /// <param name="resolution">
        /// The screen resolution to use on the Grid node.
        /// Default: <see cref="VideoResolution.R720p"/>.
        /// </param>
        /// <example>
        /// <code>
        /// var opts = new ChromeOptions();
        /// GridVideoRecorder.EnableRecording(opts, VideoResolution.R1080p);
        /// var driver = new RemoteWebDriver(new Uri("http://grid:4444"), opts);
        /// </code>
        /// </example>
        public static void EnableRecording(DriverOptions options,
            VideoResolution resolution = VideoResolution.R720p)
        {
            ArgumentNullException.ThrowIfNull(options);

            options.AddAdditionalOption("se:recordVideo",        true);
            options.AddAdditionalOption("se:screenResolution",   resolution.ToGridScreenResolution());
        }

        // ── Instance ──────────────────────────────────────────────────────────

        /// <summary>
        /// Creates a <see cref="GridVideoRecorder"/> and captures the session ID from
        /// the live driver. The driver must be a <see cref="RemoteWebDriver"/>.
        /// </summary>
        /// <param name="driver">The active <c>RemoteWebDriver</c> instance.</param>
        /// <param name="gridHubUrl">
        /// Root URL of the Selenium Grid hub (e.g. <c>"http://grid:4444"</c>).
        /// Trailing slash is optional.
        /// </param>
        /// <param name="outputDirectory">
        /// Local directory where the video file will be saved.
        /// Created automatically if it does not exist.
        /// </param>
        /// <exception cref="ArgumentNullException">Any required parameter is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">
        /// <paramref name="driver"/> is not a <see cref="RemoteWebDriver"/>.
        /// </exception>
        public GridVideoRecorder(IWebDriver driver, string gridHubUrl,
            string outputDirectory = "Recordings")
        {
            ArgumentNullException.ThrowIfNull(driver);
            ArgumentNullException.ThrowIfNull(gridHubUrl);

            if (driver is not RemoteWebDriver remote)
                throw new ArgumentException(
                    $"GridVideoRecorder requires a RemoteWebDriver, but received {driver.GetType().Name}. " +
                    "Use LocalVideoRecorder for local (non-Grid) sessions.", nameof(driver));

            _sessionId       = remote.SessionId.ToString();
            _gridHubUrl      = gridHubUrl.TrimEnd('/');
            _outputDirectory = outputDirectory;
        }

        /// <summary>
        /// Downloads the video file produced by Selenium Grid for this session.
        /// </summary>
        /// <remarks>
        /// Grid takes a few seconds to finalise the video after the session ends.
        /// This method polls with exponential back-off (2 s, 4 s, 6 s, 8 s …)
        /// until the file is available or <paramref name="timeoutSeconds"/> is exceeded.
        /// </remarks>
        /// <param name="testName">
        /// Optional base name for the saved file.
        /// <see langword="null"/> uses a UTC timestamp.
        /// </param>
        /// <param name="timeoutSeconds">
        /// Maximum total seconds to wait for the Grid to make the video available.
        /// Default: 120 s.
        /// </param>
        /// <param name="ct">Optional cancellation token.</param>
        /// <returns>
        /// The local file path on success, or <see langword="null"/> if the video
        /// was still unavailable after all retries.
        /// </returns>
        public async Task<string?> DownloadVideoAsync(
            string? testName    = null,
            int     timeoutSeconds = 120,
            CancellationToken ct   = default)
        {
            Directory.CreateDirectory(_outputDirectory);

            var fileName = string.IsNullOrWhiteSpace(testName)
                ? $"grid_{DateTime.UtcNow:yyyyMMdd_HHmmss}.mp4"
                : $"{SanitiseFileName(testName)}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.mp4";

            var destPath  = Path.Combine(_outputDirectory, fileName);
            var videoUrl  = $"{_gridHubUrl}/session/{_sessionId}/video";
            var deadline  = DateTime.UtcNow.AddSeconds(timeoutSeconds);

            // Retry with linear back-off: 2 s, 4 s, 6 s, 8 s …
            int attempt  = 0;
            int maxDelay = 30; // max single wait in seconds

            while (DateTime.UtcNow < deadline)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    using var response = await _httpClient
                        .GetAsync(videoUrl, HttpCompletionOption.ResponseHeadersRead, ct)
                        .ConfigureAwait(false);

                    if (response.IsSuccessStatusCode)
                    {
                        using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write,
                            FileShare.None, bufferSize: 81920, useAsync: true);

                        await response.Content.CopyToAsync(fileStream, ct).ConfigureAwait(false);

                        VideoPath = destPath;
                        return VideoPath;
                    }

                    Console.WriteLine(
                        $"[GridVideoRecorder] Video not ready yet (HTTP {(int)response.StatusCode}). Retrying…");
                }
                catch (HttpRequestException ex)
                {
                    Console.WriteLine($"[GridVideoRecorder] HTTP error polling for video: {ex.Message}");
                }

                attempt++;
                var waitSeconds = Math.Min(attempt * 2, maxDelay);
                var remaining   = (deadline - DateTime.UtcNow).TotalSeconds;
                var wait        = (int)Math.Min(waitSeconds, remaining);

                if (wait <= 0)
                    break;

                try { await Task.Delay(TimeSpan.FromSeconds(wait), ct).ConfigureAwait(false); }
                catch (OperationCanceledException) { throw; }
            }

            Console.WriteLine(
                $"[GridVideoRecorder] Video was not available after {timeoutSeconds} s. " +
                "The session recording may not have been enabled, or Grid is still processing.");

            return null;
        }

        private static string SanitiseFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
        }
    }
}
