namespace SimpleSeleniumSupport.Selectors
{
    /// <summary>
    /// Options for Locator action methods (Click, Fill, Hover, etc.).
    /// </summary>
    public class ActionOptions
    {
        /// <summary>
        /// Maximum seconds to wait for the element to be actionable.
        /// Defaults to <see cref="SimpleSeleniumSupportDefaults.LocatorTimeoutSeconds"/>.
        /// </summary>
        public int TimeoutSeconds { get; set; } = SimpleSeleniumSupportDefaults.LocatorTimeoutSeconds;

        /// <summary>Polling interval in milliseconds while waiting.</summary>
        public int PollingMs { get; set; } = 200;

        /// <summary>
        /// When <c>true</c>, bypasses actionability checks (visible + enabled) and acts immediately.
        /// </summary>
        public bool Force { get; set; } = false;

        /// <summary>Scroll the element into view before acting. Default: <c>true</c>.</summary>
        public bool ScrollIntoView { get; set; } = true;
    }
}
