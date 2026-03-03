namespace SimpleSeleniumSupport.Accessibility
{
    /// <summary>
    /// Options controlling which axe-core rules are run and how violations are handled.
    /// </summary>
    public sealed class AxeRunOptions
    {
        /// <summary>
        /// WCAG conformance tags to limit the audit to.
        /// Common values: <c>"wcag2a"</c>, <c>"wcag2aa"</c>, <c>"wcag21a"</c>, <c>"wcag21aa"</c>, <c>"best-practice"</c>.
        /// <para>When null, all rules are run.</para>
        /// </summary>
        public string[]? Tags { get; set; }

        /// <summary>
        /// Explicit rule IDs to run (takes precedence over <see cref="Tags"/>).
        /// </summary>
        public string[]? RunOnly { get; set; }

        /// <summary>
        /// Rule IDs to disable regardless of <see cref="Tags"/> or <see cref="RunOnly"/>.
        /// </summary>
        public string[]? DisabledRules { get; set; }

        /// <summary>
        /// CSS selector for the scope element.  Defaults to <c>"document"</c> (entire page).
        /// </summary>
        public string? Scope { get; set; }

        /// <summary>
        /// Timeout in milliseconds waiting for <c>axe.run()</c> to complete.
        /// Defaults to 30 000 ms.
        /// </summary>
        public int TimeoutMs { get; set; } = 30_000;

        /// <summary>
        /// When <see langword="true"/>, throw <see cref="AxeAccessibilityException"/> if any violations are found.
        /// </summary>
        public bool ThrowOnFail { get; set; }

        /// <summary>
        /// How axe-core is injected.  Defaults to <see cref="AxeInjectionMode.EmbeddedResource"/>.
        /// </summary>
        public AxeInjectionMode InjectionMode { get; set; } = AxeInjectionMode.EmbeddedResource;

        /// <summary>Minimum impact level to report (<c>minor</c>, <c>moderate</c>, <c>serious</c>, <c>critical</c>). Null = all.</summary>
        public string? MinImpact { get; set; }
    }

    /// <summary>Controls how axe-core script is loaded into the browser.</summary>
    public enum AxeInjectionMode
    {
        /// <summary>Load from <c>Accessibility/Resources/axe.min.js</c> embedded resource (default).</summary>
        EmbeddedResource,
        /// <summary>Inject a <c>&lt;script&gt;</c> tag pointing to the axe-core CDN.</summary>
        Cdn,
        /// <summary>Assume axe-core is already present on the page (e.g. loaded by the app under test).</summary>
        AlreadyLoaded
    }
}
