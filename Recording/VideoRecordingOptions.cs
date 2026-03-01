namespace SimpleSeleniumSupport.Recording
{
    /// <summary>
    /// Configuration for local (screenshot-based) video recording.
    /// </summary>
    public sealed class VideoRecordingOptions
    {
        /// <summary>
        /// Target output resolution. Default: <see cref="VideoResolution.R720p"/>.
        /// </summary>
        public VideoResolution Resolution { get; set; } = VideoResolution.R720p;

        /// <summary>
        /// Frames per second fed to FFmpeg. Default: <see cref="SimpleSeleniumSupportDefaults.VideoRecordingFrameRate"/>.
        /// <para>
        /// WebDriver screenshots take ~100–300 ms each, so values above 10 rarely
        /// produce a smoother video and may drop frames on slow machines.
        /// 5 fps is a reliable default for CI.
        /// </para>
        /// </summary>
        public int FrameRate { get; set; } = SimpleSeleniumSupportDefaults.VideoRecordingFrameRate;

        /// <summary>
        /// Directory where the output .mp4 file will be written.
        /// Default: <see cref="SimpleSeleniumSupportDefaults.VideoRecordingOutputDirectory"/>.
        /// The directory is created automatically if it does not exist.
        /// </summary>
        public string OutputDirectory { get; set; } = SimpleSeleniumSupportDefaults.VideoRecordingOutputDirectory;

        /// <summary>
        /// Base name for the output file.
        /// When <see langword="null"/>, a UTC timestamp is used (<c>recording_yyyyMMdd_HHmmss</c>).
        /// </summary>
        public string? TestName { get; set; }

        /// <summary>
        /// Full path to the FFmpeg executable, or just <c>"ffmpeg"</c> when FFmpeg is on the system PATH.
        /// Default: <see cref="SimpleSeleniumSupportDefaults.VideoRecordingFfmpegPath"/>.
        /// </summary>
        public string FfmpegPath { get; set; } = SimpleSeleniumSupportDefaults.VideoRecordingFfmpegPath;
    }
}
