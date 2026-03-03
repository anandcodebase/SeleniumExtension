namespace SimpleSeleniumSupport.Accessibility
{
    /// <summary>
    /// Thrown by axe-core integration when accessibility violations are found
    /// and <see cref="AxeRunOptions.ThrowOnFail"/> is <see langword="true"/>.
    /// </summary>
    public sealed class AxeAccessibilityException : Exception
    {
        /// <summary>The audit result that triggered the exception.</summary>
        public AxeResult Result { get; }

        internal AxeAccessibilityException(AxeResult result)
            : base(BuildMessage(result))
        {
            Result = result;
        }

        private static string BuildMessage(AxeResult result)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Accessibility audit found {result.ViolationCount} violation(s) on {result.Url}:");
            foreach (var v in result.Violations)
                sb.AppendLine($"  {v}");
            return sb.ToString().TrimEnd();
        }
    }


    /// <summary>
    /// Result of an axe-core accessibility audit against a single page.
    /// </summary>
    public sealed class AxeResult
    {
        /// <summary>URL of the page that was audited.</summary>
        public string Url { get; init; } = "";

        /// <summary>UTC time the audit was collected.</summary>
        public DateTime CollectedAt { get; init; } = DateTime.UtcNow;

        /// <summary>Accessibility violations found on the page.</summary>
        public IReadOnlyList<AxeViolation> Violations { get; init; } = [];

        /// <summary>Rules that passed.</summary>
        public IReadOnlyList<string> PassedRules { get; init; } = [];

        /// <summary>Rules that were not applicable to the page.</summary>
        public IReadOnlyList<string> InapplicableRules { get; init; } = [];

        /// <summary>Number of violations found.</summary>
        public int ViolationCount => Violations.Count;

        /// <summary>True when no violations were found.</summary>
        public bool Passed => Violations.Count == 0;

        /// <summary>Raw JSON returned by axe-core (for advanced inspection).</summary>
        public string RawJson { get; init; } = "";
    }

    /// <summary>A single accessibility violation reported by axe-core.</summary>
    public sealed class AxeViolation
    {
        /// <summary>Unique rule identifier (e.g. <c>color-contrast</c>).</summary>
        public string Id { get; init; } = "";

        /// <summary>Human-readable rule description.</summary>
        public string Description { get; init; } = "";

        /// <summary>Impact level: <c>minor</c>, <c>moderate</c>, <c>serious</c>, or <c>critical</c>.</summary>
        public string Impact { get; init; } = "";

        /// <summary>WCAG tags (e.g. <c>wcag2a</c>, <c>wcag2aa</c>).</summary>
        public IReadOnlyList<string> Tags { get; init; } = [];

        /// <summary>CSS selectors of the failing elements.</summary>
        public IReadOnlyList<string> TargetSelectors { get; init; } = [];

        /// <summary>URL to the axe-core help page for this rule.</summary>
        public string HelpUrl { get; init; } = "";

        public override string ToString() =>
            $"[{Impact.ToUpperInvariant()}] {Id}: {Description} ({TargetSelectors.Count} element(s))";
    }
}
