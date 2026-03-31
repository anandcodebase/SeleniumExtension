using OpenQA.Selenium;
using SimpleSeleniumSupport.Selectors;
using System;
using System.Threading;

namespace SimpleSeleniumSupport.Assertions
{
    /// <summary>
    /// Fluent assertion API for a <see cref="Locator"/>, mirroring Playwright's <c>expect(locator)</c>.
    /// All assertions auto-retry up to <see cref="SimpleSeleniumSupportDefaults.AssertionTimeoutSeconds"/>
    /// before failing, giving the DOM time to settle.
    /// </summary>
    /// <example>
    /// <code>
    /// WebExpect.That(driver.Locator("#btn")).IsVisible();
    /// WebExpect.That(driver.Locator("h1")).HasText("Welcome");
    /// WebExpect.That(driver.Locator(".badge")).HasAttribute("class", "active");
    /// WebExpect.That(driver.Locator("input")).Not.HasValue("error");
    /// </code>
    /// </example>
    public sealed class LocatorAssertion
    {
        private readonly Locator _locator;
        private readonly bool _negate;

        internal LocatorAssertion(Locator locator, bool negate = false)
        {
            _locator = locator;
            _negate = negate;
        }

        /// <summary>Inverts the assertion. <c>WebExpect.That(loc).Not.IsVisible()</c> passes if the element is hidden.</summary>
        public LocatorAssertion Not => new LocatorAssertion(_locator, !_negate);

        // ─── Visibility / state ────────────────────────────────────────────────────

        /// <summary>Asserts the element is visible (or not visible when negated).</summary>
        public void IsVisible() =>
            Poll("IsVisible", () => _locator.IsVisible(), expected: "visible");

        /// <summary>Asserts the element is hidden (or visible when negated).</summary>
        public void IsHidden() =>
            Poll("IsHidden", () => _locator.IsHidden(), expected: "hidden");

        /// <summary>Asserts the element is enabled (or disabled when negated).</summary>
        public void IsEnabled() =>
            Poll("IsEnabled", () => _locator.IsEnabled(), expected: "enabled");

        /// <summary>Asserts the element is disabled (or enabled when negated).</summary>
        public void IsDisabled() =>
            Poll("IsDisabled", () => _locator.IsDisabled(), expected: "disabled");

        /// <summary>Asserts the checkbox/radio is checked (or unchecked when negated).</summary>
        public void IsChecked() =>
            Poll("IsChecked", () => _locator.IsChecked(), expected: "checked");

        /// <summary>Asserts the element is editable (visible, enabled, no readonly).</summary>
        public void IsEditable() =>
            Poll("IsEditable", () => _locator.IsEditable(), expected: "editable");

        /// <summary>Asserts the element has no text content and no value (or has content when negated).</summary>
        public void IsEmpty() =>
            Poll("IsEmpty", () =>
            {
                var text = _locator.TextContent() ?? "";
                var value = _locator.InputValue() ?? "";
                return string.IsNullOrEmpty(text.Trim()) && string.IsNullOrEmpty(value.Trim());
            }, expected: "empty");

        // ─── Content ──────────────────────────────────────────────────────────────

        /// <summary>Asserts the element's text equals <paramref name="expected"/> (exact, case-sensitive).</summary>
        public void HasText(string expected) =>
            Poll("HasText", () =>
            {
                var actual = _locator.TextContent() ?? "";
                return string.Equals(actual.Trim(), expected.Trim(), StringComparison.Ordinal);
            }, expected, () => _locator.TextContent() ?? "<null>");

        /// <summary>Asserts the element's text contains <paramref name="substring"/> (case-insensitive).</summary>
        public void ContainsText(string substring) =>
            Poll("ContainsText", () =>
            {
                var actual = _locator.TextContent() ?? "";
                return actual.IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0;
            }, $"contains '{substring}'", () => _locator.TextContent() ?? "<null>");

        /// <summary>Asserts the input/textarea value equals <paramref name="expected"/>.</summary>
        public void HasValue(string expected) =>
            Poll("HasValue", () =>
            {
                var actual = _locator.InputValue() ?? "";
                return string.Equals(actual, expected, StringComparison.Ordinal);
            }, expected, () => _locator.InputValue() ?? "<null>");

        /// <summary>Asserts the named attribute equals <paramref name="value"/>.</summary>
        public void HasAttribute(string name, string value) =>
            Poll("HasAttribute", () =>
            {
                var actual = _locator.GetAttribute(name);
                return string.Equals(actual, value, StringComparison.Ordinal);
            }, $"{name}='{value}'", () => _locator.GetAttribute(name) ?? "<null>");

        /// <summary>Asserts the element has a CSS class containing <paramref name="className"/>.</summary>
        public void HasClass(string className) =>
            Poll("HasClass", () =>
            {
                var classAttribute = _locator.GetAttribute("class") ?? "";
                return classAttribute.Split(' ').Any(c => string.Equals(c.Trim(), className, StringComparison.Ordinal));
            }, $"class contains '{className}'",
               () => _locator.GetAttribute("class") ?? "<null>");

        /// <summary>Asserts exactly <paramref name="count"/> elements match the locator.</summary>
        public void HasCount(int count) =>
            Poll("HasCount", () => _locator.Count() == count,
                $"count={count}", () => $"count={_locator.Count()}");

        /// <summary>
        /// Asserts the element's inner HTML contains <paramref name="html"/> (case-insensitive).
        /// </summary>
        public void ContainsHTML(string html) =>
            Poll("ContainsHTML", () =>
            {
                var inner = _locator.InnerHTML() ?? "";
                return inner.IndexOf(html, StringComparison.OrdinalIgnoreCase) >= 0;
            }, $"innerHTML contains '{html}'", () => _locator.InnerHTML() ?? "<null>");

        // ─── Core retry loop ──────────────────────────────────────────────────────

        private void Poll(
            string assertionName,
            Func<bool> condition,
            string? expected = null,
            Func<string>? getActual = null)
        {
            int timeoutSeconds = SimpleSeleniumSupportDefaults.AssertionTimeoutSeconds;
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            bool result = false;

            while (DateTime.UtcNow < deadline)
            {
                try { result = condition(); }
                catch { result = false; }

                bool pass = _negate ? !result : result;
                if (pass) return;

                Thread.Sleep(200);
            }

            // Final evaluation for the error message
            bool finalResult = false;
            try { finalResult = condition(); }
            catch { /* ignore */ }

            bool finalPass = _negate ? !finalResult : finalResult;
            if (finalPass) return;

            var actualString = getActual != null ? getActual() : (result ? "true" : "false");
            var negationPrefix = _negate ? "NOT " : "";
            var errorMessage = $"Assertion failed: expected '{_locator.Description}' {negationPrefix}to {assertionName}"
                    + (expected != null ? $" [{expected}]" : "")
                    + $". Actual: {actualString}";

            throw new WebAssertionException(_locator.Description, errorMessage, expected, actualString);
        }
    }
}
