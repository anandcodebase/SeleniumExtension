using OpenQA.Selenium;
using SimpleSeleniumSupport.Selectors;
using System;
using System.Collections.Generic;

namespace SimpleSeleniumSupport.Assertions
{
    /// <summary>
    /// Collects multiple assertion failures without stopping the test on the first failure.
    /// Use in a <c>using</c> block — all failures are thrown as a single
    /// <see cref="SoftAssertionException"/> on <c>Dispose()</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// using var soft = new SoftAssertions(driver);
    ///
    /// soft.Expect(driver.Locator("h1")).HasText("Welcome");
    /// soft.Expect(driver.Locator("#badge")).HasCount(3);
    /// soft.Expect(driver).HasTitle("My App");
    ///
    /// // All failures are thrown together at end of using block
    /// </code>
    /// </example>
    public sealed class SoftAssertions : IDisposable
    {
        private readonly IWebDriver _driver;
        private readonly List<WebAssertionException> _failures = new();
        private bool _disposed;

        /// <summary>
        /// Initialises a new soft-assertion scope bound to <paramref name="driver"/>.
        /// </summary>
        public SoftAssertions(IWebDriver driver)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <summary>
        /// Creates a non-throwing <see cref="SoftLocatorAssertion"/> for the given <paramref name="locator"/>.
        /// </summary>
        public SoftLocatorAssertion Expect(Locator locator)
        {
            if (locator == null) throw new ArgumentNullException(nameof(locator));
            return new SoftLocatorAssertion(locator, _failures);
        }

        /// <summary>Creates a non-throwing <see cref="SoftPageAssertion"/> for page-level checks.</summary>
        public SoftPageAssertion Expect(IWebDriver driver)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            return new SoftPageAssertion(driver, _failures);
        }

        /// <summary>Returns the count of failures recorded so far.</summary>
        public int FailureCount => _failures.Count;

        /// <summary>
        /// Throws a <see cref="SoftAssertionException"/> if any assertions have failed.
        /// Called automatically on <c>Dispose()</c>.
        /// </summary>
        public void AssertAll()
        {
            if (_failures.Count > 0)
                throw new SoftAssertionException(new List<WebAssertionException>(_failures).AsReadOnly());
        }

        /// <summary>
        /// Calls <see cref="AssertAll"/> and then marks this instance as disposed.
        /// </summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            AssertAll();
        }
    }

    /// <summary>
    /// Non-throwing page-level assertions for use with <see cref="SoftAssertions"/>.
    /// </summary>
    public sealed class SoftPageAssertion
    {
        private readonly IWebDriver _driver;
        private readonly List<WebAssertionException> _failures;
        private readonly bool _negate;

        internal SoftPageAssertion(IWebDriver driver, List<WebAssertionException> failures, bool negate = false)
        {
            _driver = driver;
            _failures = failures;
            _negate = negate;
        }

        /// <summary>Inverts this soft assertion.</summary>
        public SoftPageAssertion Not => new SoftPageAssertion(_driver, _failures, !_negate);

        /// <summary>Soft assertion: page title equals <paramref name="expected"/>.</summary>
        public void HasTitle(string expected)           => Run(() => Inner.HasTitle(expected));
        /// <summary>Soft assertion: page title contains <paramref name="substring"/>.</summary>
        public void TitleContains(string substring)     => Run(() => Inner.TitleContains(substring));
        /// <summary>Soft assertion: current URL equals <paramref name="expected"/>.</summary>
        public void HasUrl(string expected)             => Run(() => Inner.HasUrl(expected));
        /// <summary>Soft assertion: current URL contains <paramref name="substring"/>.</summary>
        public void UrlContains(string substring)       => Run(() => Inner.UrlContains(substring));
        /// <summary>Soft assertion: current URL starts with <paramref name="prefix"/>.</summary>
        public void UrlStartsWith(string prefix)        => Run(() => Inner.UrlStartsWith(prefix));

        private PageAssertion Inner => new PageAssertion(_driver, _negate);

        private void Run(Action assertion)
        {
            try { assertion(); }
            catch (WebAssertionException ex) { _failures.Add(ex); }
        }
    }
}
