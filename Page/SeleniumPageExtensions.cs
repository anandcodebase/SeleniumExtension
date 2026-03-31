using OpenQA.Selenium;
using System;

namespace SimpleSeleniumSupport.Page
{
    /// <summary>
    /// Extension method that converts an <see cref="IWebDriver"/> into a <see cref="SeleniumPage"/>.
    /// </summary>
    public static class SeleniumPageExtensions
    {
        /// <summary>
        /// Wraps <paramref name="driver"/> in a <see cref="SeleniumPage"/> for Playwright-style
        /// navigation, wait, JS evaluation, screenshot, cookie, storage, and dialog APIs.
        /// </summary>
        /// <example>
        /// <code>
        /// var page = driver.AsPage();
        /// page.GoTo("https://example.com");
        /// page.WaitForLoadState(LoadState.NetworkIdle);
        /// WebExpect.That(driver).HasTitle("Example Domain");
        /// </code>
        /// </example>
        public static SeleniumPage AsPage(this IWebDriver driver)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            return new SeleniumPage(driver);
        }
    }
}
