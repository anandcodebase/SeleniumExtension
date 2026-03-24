namespace SimpleSeleniumSupport.AI
{
    /// <summary>
    /// Controls how failed tests are batched before being sent to the AI for analysis.
    /// Smaller groups produce more precise per-test analysis at higher token cost.
    /// Larger groups (or <see cref="AllTogether"/>) save tokens but may dilute analysis quality.
    /// </summary>
    public enum FailureGroupingStrategy
    {
        /// <summary>One AI call per failed test. Most detailed, highest token cost.</summary>
        PerTest,

        /// <summary>
        /// Group failures by <see cref="Reporting.TestResult.TestSuite"/> (class name).
        /// Good balance of cost vs. detail — failures in the same class often share a root cause.
        /// This is the default.
        /// </summary>
        PerClass,

        /// <summary>Group failures by <see cref="Reporting.TestResult.Category"/>.</summary>
        PerCategory,

        /// <summary>Group failures by <see cref="Reporting.TestResult.Browser"/>.</summary>
        PerBrowser,

        /// <summary>Group failures by the first tag in <see cref="Reporting.TestResult.Tags"/>.</summary>
        PerTag,

        /// <summary>
        /// All failed tests in a single AI call.
        /// Lowest token cost; best for quick triage of large failure sets.
        /// </summary>
        AllTogether
    }
}
