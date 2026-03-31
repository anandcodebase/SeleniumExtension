using OpenQA.Selenium;
using System;
using System.Threading;

namespace SimpleSeleniumSupport.Assertions
{
    /// <summary>
    /// Fluent assertion API for page-level properties (URL, title).
    /// Mirrors Playwright's <c>expect(page)</c> assertions.
    /// All assertions auto-retry up to <see cref="SimpleSeleniumSupportDefaults.AssertionTimeoutSeconds"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// WebExpect.That(driver).HasTitle("Dashboard");
    /// WebExpect.That(driver).HasUrl("https://app.example.com/dashboard");
    /// WebExpect.That(driver).UrlContains("/dashboard");
    /// WebExpect.That(driver).Not.HasTitle("Error");
    /// </code>
    /// </example>
    public sealed class PageAssertion
    {
        private readonly IWebDriver _driver;
        private readonly bool _negate;

        internal PageAssertion(IWebDriver driver, bool negate = false)
        {
            _driver = driver;
            _negate = negate;
        }

        /// <summary>Inverts the assertion.</summary>
        public PageAssertion Not => new PageAssertion(_driver, !_negate);

        // ─── Title ────────────────────────────────────────────────────────────────

        /// <summary>Asserts the page title equals <paramref name="expected"/> (exact, case-sensitive).</summary>
        public void HasTitle(string expected) =>
            Poll("HasTitle", () => string.Equals(_driver.Title, expected, StringComparison.Ordinal),
                expected, () => _driver.Title ?? "<null>");

        /// <summary>Asserts the page title contains <paramref name="substring"/> (case-insensitive).</summary>
        public void TitleContains(string substring) =>
            Poll("TitleContains", () =>
                (_driver.Title ?? "").IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0,
                $"contains '{substring}'", () => _driver.Title ?? "<null>");

        // ─── URL ──────────────────────────────────────────────────────────────────

        /// <summary>Asserts the current URL equals <paramref name="expected"/> (case-insensitive).</summary>
        public void HasUrl(string expected) =>
            Poll("HasUrl", () => string.Equals(_driver.Url, expected, StringComparison.OrdinalIgnoreCase),
                expected, () => _driver.Url ?? "<null>");

        /// <summary>Asserts the current URL contains <paramref name="substring"/> (case-insensitive).</summary>
        public void UrlContains(string substring) =>
            Poll("UrlContains", () =>
                (_driver.Url ?? "").IndexOf(substring, StringComparison.OrdinalIgnoreCase) >= 0,
                $"contains '{substring}'", () => _driver.Url ?? "<null>");

        /// <summary>Asserts the current URL starts with <paramref name="prefix"/> (case-insensitive).</summary>
        public void UrlStartsWith(string prefix) =>
            Poll("UrlStartsWith", () =>
                (_driver.Url ?? "").StartsWith(prefix, StringComparison.OrdinalIgnoreCase),
                $"starts with '{prefix}'", () => _driver.Url ?? "<null>");

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

            // Final check
            try { result = condition(); }
            catch { result = false; }

            bool finalPass = _negate ? !result : result;
            if (finalPass) return;

            var actualString = getActual != null ? getActual() : (result ? "true" : "false");
            var negationPrefix = _negate ? "NOT " : "";
            var errorMessage = $"Page assertion failed: expected {negationPrefix}{assertionName}"
                    + (expected != null ? $" [{expected}]" : "")
                    + $". Actual: {actualString}";

            throw new WebAssertionException("page", errorMessage, expected, actualString);
        }
    }
}
