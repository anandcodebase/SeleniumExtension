namespace SimpleSeleniumSupport.AI
{
    /// <summary>
    /// AI-determined root cause category for a test failure,
    /// produced by <see cref="TestFailureAnalyzer"/>.
    /// </summary>
    public enum FailureClassification
    {
        /// <summary>Problem in the test code, selector, assertion logic, or test data.</summary>
        TestIssue,

        /// <summary>Genuine defect in the application under test.</summary>
        ProductIssue,

        /// <summary>Intermittent failure due to timing, race conditions, or retry exhaustion.</summary>
        Flaky,

        /// <summary>CI/CD environment, browser driver, network, or missing dependency problem.</summary>
        Infrastructure,

        /// <summary>Insufficient information to determine the root cause with confidence.</summary>
        Uncertain
    }

    /// <summary>AI confidence level in the <see cref="FailureClassification"/>.</summary>
    public enum AnalysisConfidence
    {
        /// <summary>Strong textual evidence supports the classification.</summary>
        High,

        /// <summary>Some evidence supports the classification but alternatives are possible.</summary>
        Medium,

        /// <summary>Weak or ambiguous evidence; treat the classification with caution.</summary>
        Low
    }
}
