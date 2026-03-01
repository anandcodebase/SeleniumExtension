namespace SimpleSeleniumSupport.Recording
{
    /// <summary>
    /// Represents a video recorder that captures test execution.
    /// </summary>
    public interface IVideoRecorder : IDisposable
    {
        /// <summary>
        /// Full path to the recorded video file, or <see langword="null"/> if recording has
        /// not started, was discarded, or download has not yet completed (Grid).
        /// </summary>
        string? VideoPath { get; }

        /// <summary>
        /// <see langword="true"/> while the capture loop is running.
        /// </summary>
        bool IsRecording { get; }

        /// <summary>
        /// Starts recording. For local recording this launches FFmpeg and begins the
        /// screenshot-capture loop. For Grid recording this is a no-op (use
        /// <see cref="GridVideoRecorder.EnableRecording"/> before creating the driver).
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown if recording is already in progress, or if a required prerequisite
        /// (FFmpeg, <see cref="OpenQA.Selenium.ITakesScreenshot"/>) is unavailable.
        /// </exception>
        void Start();

        /// <summary>
        /// Stops recording synchronously and finalises the output file.
        /// </summary>
        /// <param name="discard">
        /// When <see langword="true"/> the output file is deleted after encoding finishes
        /// and <see cref="VideoPath"/> is set to <see langword="null"/>.
        /// </param>
        void Stop(bool discard = false);

        /// <summary>
        /// Stops recording asynchronously and finalises the output file.
        /// Prefer this overload in <c>async</c> test tear-downs.
        /// </summary>
        /// <param name="discard">
        /// When <see langword="true"/> the output file is deleted and
        /// <see cref="VideoPath"/> is set to <see langword="null"/>.
        /// </param>
        /// <param name="ct">Optional cancellation token.</param>
        Task StopAsync(bool discard = false, CancellationToken ct = default);
    }
}
