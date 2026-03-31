using OpenQA.Selenium;
using SimpleSeleniumSupport.Selectors;
using System;

namespace SimpleSeleniumSupport.Assertions
{
    /// <summary>
    /// Entry point for Playwright-style auto-retrying assertions.
    /// Use <see cref="That(Locator)"/> for element assertions and <see cref="That(IWebDriver)"/>
    /// for page-level assertions.
    /// </summary>
    /// <example>
    /// <code>
    /// // Element assertions
    /// WebExpect.That(driver.Locator("#submit")).IsVisible();
    /// WebExpect.That(driver.Locator("h1")).HasText("Welcome back");
    /// WebExpect.That(driver.Locator(".count")).HasCount(3);
    /// WebExpect.That(driver.Locator("#error")).Not.IsVisible();
    ///
    /// // Page assertions
    /// WebExpect.That(driver).HasTitle("My App");
    /// WebExpect.That(driver).UrlContains("/dashboard");
    /// </code>
    /// </example>
    public static class WebExpect
    {
        /// <summary>
        /// Creates a <see cref="LocatorAssertion"/> for the given <paramref name="locator"/>.
        /// Assertions auto-retry for up to <see cref="SimpleSeleniumSupportDefaults.AssertionTimeoutSeconds"/>
        /// seconds before failing.
        /// </summary>
        public static LocatorAssertion That(Locator locator)
        {
            if (locator == null) throw new ArgumentNullException(nameof(locator));
            return new LocatorAssertion(locator);
        }

        /// <summary>
        /// Creates a <see cref="PageAssertion"/> for page-level checks (title, URL).
        /// </summary>
        public static PageAssertion That(IWebDriver driver)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            return new PageAssertion(driver);
        }
    }
}
