namespace SimpleSeleniumSupport.Performance
{
    /// <summary>
    /// Performance metrics collected from the browser for a single page load.
    /// Metrics are gathered via <c>window.performance.timing</c>,
    /// <c>PerformancePaintTiming</c>, and <c>LargestContentfulPaint</c> observer.
    /// </summary>
    public sealed record PerformanceResult
    {
        public string   Url                          { get; init; } = "";
        public DateTime CollectedAt                  { get; init; } = DateTime.UtcNow;

        // Navigation Timing metrics (all nullable — may not be available for cached/offline pages)
        public double? NavigationStartMs             { get; init; }
        public double? DnsLookupMs                   { get; init; }
        public double? ConnectionMs                  { get; init; }
        public double? ServerResponseMs              { get; init; }   // TTFB
        public double? DomInteractiveMs              { get; init; }
        public double? DomContentLoadedMs            { get; init; }
        public double? FullPageLoadMs                { get; init; }

        // Paint Timing
        public double? FirstPaintMs                  { get; init; }
        public double? FirstContentfulPaintMs        { get; init; }

        // LCP (populated if observer data was injected before navigation)
        public double? LargestContentfulPaintMs      { get; init; }

        // Budget evaluation
        public bool          BudgetPassed            { get; init; } = true;
        public List<string>  BudgetViolations        { get; init; } = new();
    }
}
