using OpenQA.Selenium;
using System.Text.Json;

namespace SimpleSeleniumSupport.Performance
{
    /// <summary>
    /// Collects Navigation Timing Level 2, Paint Timing, and LCP metrics
    /// from the browser via <c>window.performance</c> JavaScript APIs.
    /// </summary>
    internal static class NavigationTimingCollector
    {
        // JS that returns a JSON object with all relevant timing values
        private const string CollectionScript = """
            (function() {
                var t = performance.timing;
                var nav = performance.getEntriesByType('navigation')[0];
                var paints = {};
                performance.getEntriesByType('paint').forEach(function(e){ paints[e.name] = e.startTime; });
                var lcp = window.__sss_lcp_time || null;
                var base = nav ? nav.startTime : 0;
                function ms(a,b){ return (a && b && b>a) ? Math.round((b-a)*10)/10 : null; }
                return JSON.stringify({
                    url:                document.URL,
                    dnsLookup:          ms(t.domainLookupStart, t.domainLookupEnd),
                    connection:         ms(t.connectStart,       t.connectEnd),
                    serverResponse:     ms(t.requestStart,       t.responseStart),
                    domInteractive:     nav ? Math.round(nav.domInteractive*10)/10 : ms(t.navigationStart, t.domInteractive),
                    domContentLoaded:   nav ? Math.round(nav.domContentLoadedEventEnd*10)/10 : ms(t.navigationStart, t.domContentLoadedEventEnd),
                    fullPageLoad:       nav ? Math.round(nav.loadEventEnd*10)/10 : ms(t.navigationStart, t.loadEventEnd),
                    firstPaint:         paints['first-paint'] || null,
                    firstContentfulPaint: paints['first-contentful-paint'] || null,
                    lcp:                lcp
                });
            })()
            """;

        /// <summary>
        /// Collects metrics and evaluates them against an optional <see cref="PerformanceBudget"/>.
        /// </summary>
        internal static PerformanceResult Collect(IWebDriver driver, PerformanceBudget? budget)
        {
            var jsExec = (IJavaScriptExecutor)driver;
            var raw    = jsExec.ExecuteScript(CollectionScript) as string ?? "{}";

            using var doc  = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            double? Get(string key) => root.TryGetProperty(key, out var p) && p.ValueKind == JsonValueKind.Number
                ? p.GetDouble() : null;
            string  Str(string key) => root.TryGetProperty(key, out var p) ? p.GetString() ?? "" : "";

            var result = new PerformanceResult
            {
                Url                      = Str("url"),
                CollectedAt              = DateTime.UtcNow,
                DnsLookupMs              = Get("dnsLookup"),
                ConnectionMs             = Get("connection"),
                ServerResponseMs         = Get("serverResponse"),
                DomInteractiveMs         = Get("domInteractive"),
                DomContentLoadedMs       = Get("domContentLoaded"),
                FullPageLoadMs           = Get("fullPageLoad"),
                FirstPaintMs             = Get("firstPaint"),
                FirstContentfulPaintMs   = Get("firstContentfulPaint"),
                LargestContentfulPaintMs = Get("lcp")
            };

            if (budget == null) return result;

            var violations = new List<string>();
            Check(violations, "First Contentful Paint", result.FirstContentfulPaintMs, budget.MaxFirstContentfulPaintMs);
            Check(violations, "Largest Contentful Paint", result.LargestContentfulPaintMs, budget.MaxLargestContentfulPaintMs);
            Check(violations, "DOM Content Loaded", result.DomContentLoadedMs, budget.MaxDomContentLoadedMs);
            Check(violations, "Full Page Load", result.FullPageLoadMs, budget.MaxFullPageLoadMs);
            Check(violations, "DNS Lookup", result.DnsLookupMs, budget.MaxDnsLookupMs);
            Check(violations, "Connection", result.ConnectionMs, budget.MaxConnectionMs);
            Check(violations, "Server Response (TTFB)", result.ServerResponseMs, budget.MaxServerResponseMs);

            return result with { BudgetPassed = violations.Count == 0, BudgetViolations = violations };
        }

        private static void Check(List<string> violations, string name, double? actual, double? max)
        {
            if (actual.HasValue && max.HasValue && actual.Value > max.Value)
                violations.Add($"{name}: {actual.Value:F0}ms (budget: {max.Value:F0}ms)");
        }
    }
}
