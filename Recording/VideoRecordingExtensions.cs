using OpenQA.Selenium;

namespace SimpleSeleniumSupport.Recording
{
    /// <summary>
    /// Extension methods on <see cref="IWebDriver"/> for starting local or Grid video recording.
    /// </summary>
    public static class VideoRecordingExtensions
    {
        /// <summary>
        /// Creates a <see cref="LocalVideoRecorder"/>, starts it immediately, and returns it.
        /// </summary>
        /// <remarks>
        /// Uses <see cref="ITakesScreenshot.GetScreenshot"/> to capture frames, so it works in
        /// headless and headed Chrome/Edge/Firefox — no OS display capture required.
        /// Call <see cref="IVideoRecorder.Stop"/> (or dispose) when the test is done.
        /// </remarks>
        /// <param name="driver">The active WebDriver. Must implement <see cref="ITakesScreenshot"/>.</param>
        /// <param name="options">
        /// Recording options. <see langword="null"/> uses library defaults
        /// (<see cref="VideoRecordingOptions"/> with values from <see cref="SimpleSeleniumSupportDefaults"/>).
        /// </param>
        /// <returns>The started recorder. Assign to a <c>using</c> variable to ensure cleanup.</returns>
        /// <example>
        /// <code>
        /// using var recorder = driver.StartLocalRecording(new VideoRecordingOptions
        /// {
        ///     Resolution      = VideoResolution.R720p,
        ///     TestName        = "CheckoutFlow",
        ///     OutputDirectory = "Recordings"
        /// });
        ///
        /// // … run test …
        ///
        /// recorder.Stop();
        /// result.ScreencastPath = recorder.VideoPath;
        /// </code>
        /// </example>
        public static IVideoRecorder StartLocalRecording(
            this IWebDriver driver, VideoRecordingOptions? options = null)
        {
            var recorder = new LocalVideoRecorder(driver, options);
            recorder.Start();
            return recorder;
        }

        /// <summary>
        /// Creates a <see cref="GridVideoRecorder"/> bound to this driver's session.
        /// The recorder captures the session ID immediately, so call this <b>before</b>
        /// calling <c>driver.Quit()</c>.
        /// </summary>
        /// <remarks>
        /// Server-side recording must be enabled before the session is created via
        /// <see cref="GridVideoRecorder.EnableRecording"/>. Call
        /// <see cref="GridVideoRecorder.DownloadVideoAsync"/> after <c>Quit()</c>.
        /// </remarks>
        /// <param name="driver">
        /// The active <c>RemoteWebDriver</c> (throws <see cref="ArgumentException"/>
        /// if a local driver is passed).
        /// </param>
        /// <param name="gridHubUrl">
        /// Root URL of the Selenium Grid hub, e.g. <c>"http://grid:4444"</c>.
        /// </param>
        /// <param name="outputDirectory">
        /// Local directory where the downloaded video will be saved.
        /// Defaults to <c>"Recordings"</c>.
        /// </param>
        /// <returns>A <see cref="GridVideoRecorder"/> ready to call <c>DownloadVideoAsync</c>.</returns>
        /// <example>
        /// <code>
        /// // ① Enable recording on the hub before session creation
        /// var opts = new ChromeOptions();
        /// GridVideoRecorder.EnableRecording(opts, VideoResolution.R1080p);
        /// var driver = new RemoteWebDriver(new Uri("http://grid:4444"), opts);
        ///
        /// // ② Create the downloader while the session is still alive
        /// var recorder = driver.CreateGridRecorder("http://grid:4444", "Recordings");
        ///
        /// // ③ Run test, then end session
        /// driver.Quit();
        ///
        /// // ④ Download (Grid needs a few seconds to finalise)
        /// string? videoPath = await recorder.DownloadVideoAsync("CheckoutFlow");
        /// result.ScreencastPath = videoPath;
        /// </code>
        /// </example>
        public static GridVideoRecorder CreateGridRecorder(
            this IWebDriver driver, string gridHubUrl, string outputDirectory = "Recordings")
            => new GridVideoRecorder(driver, gridHubUrl, outputDirectory);
    }
}
