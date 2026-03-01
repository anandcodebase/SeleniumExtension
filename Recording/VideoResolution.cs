namespace SimpleSeleniumSupport.Recording
{
    /// <summary>
    /// Target output resolution for video recording.
    /// </summary>
    public enum VideoResolution
    {
        /// <summary>640 × 360 — low bandwidth, fast encoding.</summary>
        R360p,

        /// <summary>1280 × 720 — HD, recommended default.</summary>
        R720p,

        /// <summary>1920 × 1080 — Full HD, larger file size.</summary>
        R1080p
    }

    internal static class VideoResolutionHelper
    {
        /// <summary>Returns the pixel dimensions (width, height) for the given resolution.</summary>
        internal static (int Width, int Height) ToDimensions(this VideoResolution resolution)
            => resolution switch
            {
                VideoResolution.R360p  => (640,  360),
                VideoResolution.R720p  => (1280, 720),
                VideoResolution.R1080p => (1920, 1080),
                _                      => (1280, 720)
            };

        /// <summary>
        /// Returns an FFmpeg <c>-vf</c> scale filter string using lanczos resampling.
        /// Example: <c>"scale=1280:720:flags=lanczos"</c>
        /// </summary>
        internal static string ToScaleFilter(this VideoResolution resolution)
        {
            var (w, h) = resolution.ToDimensions();
            return $"scale={w}:{h}:flags=lanczos";
        }

        /// <summary>
        /// Returns the Selenium Grid 4 <c>se:screenResolution</c> capability value.
        /// Example: <c>"1280x720x24"</c>
        /// </summary>
        internal static string ToGridScreenResolution(this VideoResolution resolution)
        {
            var (w, h) = resolution.ToDimensions();
            return $"{w}x{h}x24";
        }
    }
}
