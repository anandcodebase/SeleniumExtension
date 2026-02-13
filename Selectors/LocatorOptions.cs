namespace SimpleSeleniumSupport.Selectors
{
    /// <summary>
    /// Locator Options
    /// </summary>
    public class LocatorOptions
    {
        /// <summary>
        /// Gets or sets the timeout seconds.
        /// </summary>
        /// <value>
        /// The timeout seconds.
        /// </value>
        public int TimeoutSeconds { get; set; } = 10;
        /// <summary>
        /// Gets or sets a value indicating whether [exact match].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [exact match]; otherwise, <c>false</c>.
        /// </value>
        public bool ExactMatch { get; set; } = false;
        /// <summary>
        /// Gets or sets the wait.
        /// </summary>
        /// <value>
        /// The wait.
        /// </value>
        public WaitUntil Wait { get; set; } = WaitUntil.Visible;
        /// <summary>
        /// Gets or sets a value indicating whether [case sensitive].
        /// </summary>
        /// <value>
        ///   <c>true</c> if [case sensitive]; otherwise, <c>false</c>.
        /// </value>
        public bool CaseSensitive { get; set; } = false;
        /// <summary>
        /// Gets or sets the polling ms.
        /// </summary>
        /// <value>
        /// The polling ms.
        /// </value>
        public int PollingMs { get; set; } = 200;
    }
}
