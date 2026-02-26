namespace SimpleSeleniumSupport
{
    /// <summary>
    /// Global default configuration values for SimpleSeleniumSupport.
    /// Set these once at test-suite startup to affect all timeouts across the library.
    /// </summary>
    /// <example>
    /// // In your test setup:
    /// SimpleSeleniumSupportDefaults.WaitTimeoutSeconds = 20;
    /// SimpleSeleniumSupportDefaults.LocatorTimeoutSeconds = 15;
    /// </example>
    public static class SimpleSeleniumSupportDefaults
    {
        /// <summary>
        /// Default timeout (seconds) used by AI-powered wait operations such as
        /// <c>WaitAndFindByAI</c>. Also used as the initial value of
        /// <see cref="AI.AIElementFinder.DefaultWaitTimeoutSeconds"/>.
        /// </summary>
        public static int WaitTimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// Default timeout (seconds) used by locator wait helpers
        /// (<c>GetByRole</c>, <c>GetByText</c>, etc.).
        /// New <see cref="Selectors.LocatorOptions"/> instances read this value.
        /// </summary>
        public static int LocatorTimeoutSeconds { get; set; } = 10;

        /// <summary>
        /// Default timeout (seconds) used by <c>WaitForRequest</c> and
        /// <c>WaitForAllRequests</c> network operations.
        /// </summary>
        public static int NetworkWaitTimeoutSeconds { get; set; } = 30;
    }
}
