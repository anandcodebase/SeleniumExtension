namespace SimpleSeleniumSupport.Performance
{
    /// <summary>
    /// Defines maximum allowed durations (in milliseconds) for common web performance metrics.
    /// Any property left <see langword="null"/> is not evaluated against the budget.
    /// <para>
    /// Based on Google Core Web Vitals and W3C Navigation Timing Level 2 recommendations.
    /// </para>
    /// </summary>
    public sealed class PerformanceBudget
    {
        /// <summary>First Contentful Paint — recommended &lt; 1 800 ms.</summary>
        public double? MaxFirstContentfulPaintMs { get; set; }

        /// <summary>Largest Contentful Paint — recommended &lt; 2 500 ms.</summary>
        public double? MaxLargestContentfulPaintMs { get; set; }

        /// <summary>DOM Content Loaded event end.</summary>
        public double? MaxDomContentLoadedMs { get; set; }

        /// <summary>Full page load (window.onload).</summary>
        public double? MaxFullPageLoadMs { get; set; }

        /// <summary>DNS lookup time.</summary>
        public double? MaxDnsLookupMs { get; set; }

        /// <summary>TCP connection time.</summary>
        public double? MaxConnectionMs { get; set; }

        /// <summary>Time to First Byte (server response time).</summary>
        public double? MaxServerResponseMs { get; set; }

        /// <summary>Creates a budget matching Google's "Good" Core Web Vitals thresholds.</summary>
        public static PerformanceBudget GoogleGoodThresholds() => new()
        {
            MaxFirstContentfulPaintMs    = 1_800,
            MaxLargestContentfulPaintMs  = 2_500,
            MaxDomContentLoadedMs        = 3_000,
            MaxFullPageLoadMs            = 5_000,
            MaxServerResponseMs          = 600
        };
    }
}
