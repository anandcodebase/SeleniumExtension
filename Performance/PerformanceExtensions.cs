using OpenQA.Selenium;

namespace SimpleSeleniumSupport.Performance
{
    /// <summary>
    /// Extension methods on <see cref="IWebDriver"/> for collecting web performance metrics
    /// and evaluating them against a <see cref="PerformanceBudget"/>.
    /// </summary>
    public static class PerformanceExtensions
    {
        /// <summary>
        /// Collects Navigation Timing, Paint Timing, and LCP metrics from the current page.
        /// Optionally evaluates the metrics against a <see cref="PerformanceBudget"/>.
        /// </summary>
        /// <param name="driver">The WebDriver instance (must support <see cref="IJavaScriptExecutor"/>).</param>
        /// <param name="budget">
        /// Optional budget. When provided, <see cref="PerformanceResult.BudgetPassed"/> and
        /// <see cref="PerformanceResult.BudgetViolations"/> are populated.
        /// </param>
        /// <returns>Collected metrics and optional budget evaluation.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the driver does not implement <see cref="IJavaScriptExecutor"/>.
        /// </exception>
        public static PerformanceResult CollectPerformanceMetrics(
            this IWebDriver driver,
            PerformanceBudget? budget = null)
        {
            if (driver is not IJavaScriptExecutor)
                throw new InvalidOperationException(
                    $"The supplied IWebDriver ({driver.GetType().Name}) does not implement IJavaScriptExecutor. " +
                    "CollectPerformanceMetrics requires a browser-backed driver (Chrome, Firefox, Edge).");

            return NavigationTimingCollector.Collect(driver, budget);
        }

        /// <summary>
        /// Async version of <see cref="CollectPerformanceMetrics"/>.
        /// Runs the metric collection on a background thread to avoid blocking.
        /// </summary>
        public static Task<PerformanceResult> CollectPerformanceMetricsAsync(
            this IWebDriver driver,
            PerformanceBudget? budget = null,
            CancellationToken ct = default)
            => Task.Run(() => driver.CollectPerformanceMetrics(budget), ct);

        /// <summary>
        /// Collects metrics and throws <see cref="PerformanceBudgetException"/> if the budget is violated.
        /// </summary>
        /// <param name="driver">The WebDriver instance.</param>
        /// <param name="budget">The performance budget to enforce.</param>
        /// <exception cref="PerformanceBudgetException">Thrown when any budget threshold is exceeded.</exception>
        public static PerformanceResult AssertPerformanceBudget(
            this IWebDriver driver,
            PerformanceBudget budget)
        {
            var result = driver.CollectPerformanceMetrics(budget);
            if (!result.BudgetPassed)
                throw new PerformanceBudgetException(result);
            return result;
        }
    }

    /// <summary>Thrown by <see cref="PerformanceExtensions.AssertPerformanceBudget"/> on budget violations.</summary>
    public sealed class PerformanceBudgetException : Exception
    {
        public PerformanceResult Result { get; }

        public PerformanceBudgetException(PerformanceResult result)
            : base($"Performance budget violated ({result.BudgetViolations.Count} violation(s)):\n" +
                   string.Join("\n", result.BudgetViolations))
        {
            Result = result;
        }
    }
}
