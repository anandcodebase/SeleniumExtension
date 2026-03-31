using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using SeleniumBy = OpenQA.Selenium.By;

namespace SimpleSeleniumSupport.Selectors
{
    /// <summary>
    /// Extends <see cref="IWebDriver"/> with <see cref="Locator"/>-returning factory methods,
    /// enabling the Playwright-style lazy / auto-waiting API alongside the existing eager
    /// <c>GetByRole</c> / <c>GetByText</c> / <c>GetByTestId</c> methods.
    /// </summary>
    /// <example>
    /// <code>
    /// // Primary entry point — CSS or XPath selector
    /// driver.Locator("[data-testid='submit']").Click();
    /// driver.Locator("//button[text()='Save']").Click();
    ///
    /// // GetBy* convenience overloads (return Locator, not IWebElement)
    /// driver.LocatorByRole("button", "Submit").Click();
    /// driver.LocatorByText("Sign in").IsVisible();
    /// driver.LocatorByTestId("username").Fill("alice");
    ///
    /// // Chaining
    /// driver.Locator("form.checkout")
    ///       .GetByRole("button", "Pay Now")
    ///       .Click();
    /// </code>
    /// </example>
    public static class LocatorDriverExtensions
    {
        /// <summary>
        /// Creates a <see cref="Locator"/> that matches <paramref name="selector"/>.
        /// Selectors starting with <c>/</c> or <c>(</c> are treated as XPath; everything
        /// else is treated as a CSS selector.
        /// </summary>
        public static Locator Locator(this IWebDriver driver, string selector)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            bool isXPath = selector.TrimStart().StartsWith("/") || selector.TrimStart().StartsWith("(");
            SeleniumBy by = isXPath ? SeleniumBy.XPath(selector) : SeleniumBy.CssSelector(selector);
            return new Locator(driver,
                ctx => ctx.FindElements(by).ToList().AsReadOnly(),
                selector);
        }

        /// <summary>
        /// Creates a <see cref="Locator"/> using an existing Selenium <see cref="SeleniumBy"/>.
        /// </summary>
        public static Locator Locator(this IWebDriver driver, SeleniumBy by)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            return new Locator(driver,
                ctx => ctx.FindElements(by).ToList().AsReadOnly(),
                by.ToString());
        }

        /// <summary>
        /// Creates a <see cref="Locator"/> using a WAI-ARIA role, optionally filtered by accessible name.
        /// Returns a <see cref="Locator"/> (lazy) rather than an <see cref="IWebElement"/> (eager).
        /// </summary>
        public static Locator LocatorByRole(
            this IWebDriver driver,
            string role,
            string? accessibleName = null,
            LocatorOptions? options = null)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            options ??= new LocatorOptions();
            var by = new ByRole(role, accessibleName, options);
            return new Locator(driver,
                ctx => ctx.FindElements(by).ToList().AsReadOnly(),
                $"role={role}{(accessibleName != null ? $"[{accessibleName}]" : "")}");
        }

        /// <summary>
        /// Creates a <see cref="Locator"/> by text content.
        /// Returns a <see cref="Locator"/> (lazy) rather than an <see cref="IWebElement"/> (eager).
        /// </summary>
        public static Locator LocatorByText(
            this IWebDriver driver,
            string text,
            LocatorOptions? options = null)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            options ??= new LocatorOptions();
            var by = new ByText(text, options);
            return new Locator(driver,
                ctx => ctx.FindElements(by).ToList().AsReadOnly(),
                $"text='{text}'");
        }

        /// <summary>
        /// Creates a <see cref="Locator"/> by <c>data-testid</c> attribute.
        /// Returns a <see cref="Locator"/> (lazy) rather than an <see cref="IWebElement"/> (eager).
        /// </summary>
        public static Locator LocatorByTestId(
            this IWebDriver driver,
            string testId,
            LocatorOptions? options = null)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            options ??= new LocatorOptions();
            var by = new ByTestId(testId, options);
            return new Locator(driver,
                ctx => ctx.FindElements(by).ToList().AsReadOnly(),
                $"testid='{testId}'");
        }

        /// <summary>
        /// Creates a <see cref="Locator"/> by label text.
        /// Returns a <see cref="Locator"/> (lazy) rather than an <see cref="IWebElement"/> (eager).
        /// </summary>
        public static Locator LocatorByLabel(
            this IWebDriver driver,
            string labelText,
            int timeoutSeconds = -1)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            return new Locator(driver, ctx =>
            {
                if (ctx is IWebDriver webDriver)
                {
                    if (webDriver.TryGetByLabel(labelText, out var element, timeoutSeconds <= 0 ? 1 : timeoutSeconds))
                        return new List<IWebElement> { element }.AsReadOnly();
                }
                return Array.Empty<IWebElement>();
            }, $"label='{labelText}'");
        }

        /// <summary>
        /// Creates a <see cref="Locator"/> by placeholder attribute.
        /// Returns a <see cref="Locator"/> (lazy) rather than an <see cref="IWebElement"/> (eager).
        /// </summary>
        public static Locator LocatorByPlaceholder(
            this IWebDriver driver,
            string placeholder,
            int timeoutSeconds = -1)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            return new Locator(driver, ctx =>
            {
                if (ctx is IWebDriver webDriver)
                {
                    if (webDriver.TryGetByPlaceholder(placeholder, out var element, timeoutSeconds <= 0 ? 1 : timeoutSeconds))
                        return new List<IWebElement> { element }.AsReadOnly();
                }
                return Array.Empty<IWebElement>();
            }, $"placeholder='{placeholder}'");
        }

        /// <summary>
        /// Creates a <see cref="Locator"/> by alt attribute.
        /// Returns a <see cref="Locator"/> (lazy) rather than an <see cref="IWebElement"/> (eager).
        /// </summary>
        public static Locator LocatorByAlt(
            this IWebDriver driver,
            string altText,
            int timeoutSeconds = -1)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            return new Locator(driver, ctx =>
            {
                if (ctx is IWebDriver webDriver)
                {
                    if (webDriver.TryGetByAlt(altText, out var element, timeoutSeconds <= 0 ? 1 : timeoutSeconds))
                        return new List<IWebElement> { element }.AsReadOnly();
                }
                return Array.Empty<IWebElement>();
            }, $"alt='{altText}'");
        }

        /// <summary>
        /// Creates a <see cref="Locator"/> by title attribute.
        /// Returns a <see cref="Locator"/> (lazy) rather than an <see cref="IWebElement"/> (eager).
        /// </summary>
        public static Locator LocatorByTitle(
            this IWebDriver driver,
            string title,
            int timeoutSeconds = -1)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            return new Locator(driver, ctx =>
            {
                if (ctx is IWebDriver webDriver)
                {
                    if (webDriver.TryGetByTitle(title, out var element, timeoutSeconds <= 0 ? 1 : timeoutSeconds))
                        return new List<IWebElement> { element }.AsReadOnly();
                }
                return Array.Empty<IWebElement>();
            }, $"title='{title}'");
        }

        /// <summary>
        /// Returns a <see cref="FrameLocator"/> scoped to the iframe matched by <paramref name="frameSelector"/>.
        /// </summary>
        public static FrameLocator FrameLocator(this IWebDriver driver, string frameSelector)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            return new FrameLocator(driver, null, frameSelector);
        }
    }
}
