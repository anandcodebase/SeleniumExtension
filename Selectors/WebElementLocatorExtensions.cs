using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSeleniumSupport.Selectors
{
    /// <summary>
    /// Scoped locator extensions for IWebElement.
    /// Mirrors the IWebDriver API (GetByRole, GetByText, GetByTestId etc.)
    /// but searches within the element's subtree.
    /// All methods support LocatorOptions for wait/timeout/polling.
    /// </summary>
    public static class WebElementLocatorExtensions
    {
        #region ByRole

        /// <summary>
        /// Finds an element by ARIA role within this element. Throws if not found.
        /// </summary>
        public static IWebElement GetByRole(this IWebElement element, string role, string accessibleName = null, LocatorOptions options = null)
        {
            var el = element.TryGetByRole(role, accessibleName, options);
            if (el == null)
                throw new NoSuchElementException($"No element found with role='{role}' name='{accessibleName}'");
            return el;
        }

        /// <summary>
        /// Finds an element by ARIA role within this element. Returns null if not found.
        /// </summary>
        public static IWebElement TryGetByRole(this IWebElement element, string role, string accessibleName = null, LocatorOptions options = null)
        {
            options ??= new LocatorOptions();
            var by = new ByRole(role, accessibleName, options);
            return LocatorWaitHelper.FindElementWithWait(element, by, options, throwOnTimeout: false);
        }

        /// <summary>
        /// Finds an element by ARIA role within this element. Returns result via out parameter.
        /// </summary>
        public static bool TryGetByRole(this IWebElement element, string role, out IWebElement found, string accessibleName = null, LocatorOptions options = null)
        {
            found = element.TryGetByRole(role, accessibleName, options);
            return found != null;
        }

        /// <summary>
        /// Finds all elements by ARIA role within this element. Throws if none found.
        /// </summary>
        public static IReadOnlyCollection<IWebElement> GetAllByRole(this IWebElement element, string role, string accessibleName = null, LocatorOptions options = null)
        {
            options ??= new LocatorOptions();
            var by = new ByRole(role, accessibleName, options);
            var list = LocatorWaitHelper.FindElementsWithWait(element, by, options, throwOnTimeout: false);
            if (list == null || list.Count == 0)
                throw new NoSuchElementException($"No elements found with role='{role}' name='{accessibleName}'");
            return list;
        }

        /// <summary>
        /// Finds all elements by ARIA role within this element. Returns empty if none found.
        /// </summary>
        public static IReadOnlyCollection<IWebElement> TryGetAllByRole(this IWebElement element, string role, string accessibleName = null, LocatorOptions options = null)
        {
            options ??= new LocatorOptions();
            var by = new ByRole(role, accessibleName, options);
            return LocatorWaitHelper.FindElementsWithWait(element, by, options, throwOnTimeout: false);
        }

        #endregion

        #region ByText

        /// <summary>
        /// Finds an element by text content within this element. Throws if not found.
        /// </summary>
        public static IWebElement GetByText(this IWebElement element, string text, LocatorOptions options = null)
        {
            var el = element.TryGetByText(text, options);
            if (el == null)
                throw new NoSuchElementException($"No element found with text '{text}'");
            return el;
        }

        /// <summary>
        /// Finds an element by text content within this element. Returns null if not found.
        /// </summary>
        public static IWebElement TryGetByText(this IWebElement element, string text, LocatorOptions options = null)
        {
            options ??= new LocatorOptions();
            var by = new ByText(text, options);
            return LocatorWaitHelper.FindElementWithWait(element, by, options, throwOnTimeout: false);
        }

        /// <summary>
        /// Finds an element by text content within this element. Returns result via out parameter.
        /// </summary>
        public static bool TryGetByText(this IWebElement element, string text, out IWebElement found, LocatorOptions options = null)
        {
            found = element.TryGetByText(text, options);
            return found != null;
        }

        /// <summary>
        /// Finds all elements by text content within this element. Throws if none found.
        /// </summary>
        public static IReadOnlyCollection<IWebElement> GetAllByText(this IWebElement element, string text, LocatorOptions options = null)
        {
            options ??= new LocatorOptions();
            var by = new ByText(text, options);
            var list = LocatorWaitHelper.FindElementsWithWait(element, by, options, throwOnTimeout: false);
            if (list == null || list.Count == 0)
                throw new NoSuchElementException($"No elements found with text '{text}'");
            return list;
        }

        /// <summary>
        /// Finds all elements by text content within this element. Returns empty if none found.
        /// </summary>
        public static IReadOnlyCollection<IWebElement> TryGetAllByText(this IWebElement element, string text, LocatorOptions options = null)
        {
            options ??= new LocatorOptions();
            var by = new ByText(text, options);
            return LocatorWaitHelper.FindElementsWithWait(element, by, options, throwOnTimeout: false);
        }

        #endregion

        #region ByTestId

        /// <summary>
        /// Finds an element by test ID within this element. Throws if not found.
        /// </summary>
        public static IWebElement GetByTestId(this IWebElement element, string testId, LocatorOptions options = null)
        {
            var el = element.TryGetByTestId(testId, options);
            if (el == null)
                throw new NoSuchElementException($"No element found with testId='{testId}'");
            return el;
        }

        /// <summary>
        /// Finds an element by test ID within this element. Returns null if not found.
        /// </summary>
        public static IWebElement TryGetByTestId(this IWebElement element, string testId, LocatorOptions options = null)
        {
            options ??= new LocatorOptions();
            var by = new ByTestId(testId, options);
            return LocatorWaitHelper.FindElementWithWait(element, by, options, throwOnTimeout: false);
        }

        /// <summary>
        /// Finds all elements by test ID within this element. Throws if none found.
        /// </summary>
        public static IReadOnlyCollection<IWebElement> GetAllByTestId(this IWebElement element, string testId, LocatorOptions options = null)
        {
            options ??= new LocatorOptions();
            var by = new ByTestId(testId, options);
            var list = LocatorWaitHelper.FindElementsWithWait(element, by, options, throwOnTimeout: false);
            if (list == null || list.Count == 0)
                throw new NoSuchElementException($"No elements found with testId='{testId}'");
            return list;
        }

        /// <summary>
        /// Finds all elements by test ID within this element. Returns empty if none found.
        /// </summary>
        public static IReadOnlyCollection<IWebElement> TryGetAllByTestId(this IWebElement element, string testId, LocatorOptions options = null)
        {
            options ??= new LocatorOptions();
            var by = new ByTestId(testId, options);
            return LocatorWaitHelper.FindElementsWithWait(element, by, options, throwOnTimeout: false);
        }

        #endregion

        #region Element waits

        /// <summary>
        /// Waits until this element is visible (Displayed == true).
        /// Returns the element so calls can be chained.
        /// </summary>
        /// <param name="element">The element to wait on.</param>
        /// <param name="timeoutSeconds">
        /// Maximum wait time. Defaults to
        /// <see cref="SimpleSeleniumSupportDefaults.LocatorTimeoutSeconds"/>.
        /// </param>
        /// <param name="pollingMs">How often to poll. Defaults to 200 ms.</param>
        /// <returns>The same element, for fluent chaining.</returns>
        /// <exception cref="WebDriverTimeoutException">
        /// Thrown if the element is not visible within the timeout.
        /// </exception>
        public static IWebElement WaitUntilVisible(
            this IWebElement element,
            int timeoutSeconds = -1,
            int pollingMs = 200)
        {
            if (timeoutSeconds <= 0)
                timeoutSeconds = SimpleSeleniumSupportDefaults.LocatorTimeoutSeconds;

            var wait = new DefaultWait<IWebElement>(element)
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds),
                PollingInterval = TimeSpan.FromMilliseconds(Math.Max(50, pollingMs))
            };
            wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));

            try
            {
                wait.Until(el => LocatorWaitHelper.SafeDisplayed(el));
            }
            catch (WebDriverTimeoutException)
            {
                throw new WebDriverTimeoutException(
                    $"Element was not visible after {timeoutSeconds}s.");
            }

            return element;
        }

        /// <summary>
        /// Waits until this element is both visible and enabled (Displayed &amp;&amp; Enabled).
        /// Returns the element so calls can be chained.
        /// </summary>
        /// <param name="element">The element to wait on.</param>
        /// <param name="timeoutSeconds">
        /// Maximum wait time. Defaults to
        /// <see cref="SimpleSeleniumSupportDefaults.LocatorTimeoutSeconds"/>.
        /// </param>
        /// <param name="pollingMs">How often to poll. Defaults to 200 ms.</param>
        /// <returns>The same element, for fluent chaining.</returns>
        /// <exception cref="WebDriverTimeoutException">
        /// Thrown if the element is not clickable within the timeout.
        /// </exception>
        public static IWebElement WaitUntilClickable(
            this IWebElement element,
            int timeoutSeconds = -1,
            int pollingMs = 200)
        {
            if (timeoutSeconds <= 0)
                timeoutSeconds = SimpleSeleniumSupportDefaults.LocatorTimeoutSeconds;

            var wait = new DefaultWait<IWebElement>(element)
            {
                Timeout = TimeSpan.FromSeconds(timeoutSeconds),
                PollingInterval = TimeSpan.FromMilliseconds(Math.Max(50, pollingMs))
            };
            wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));

            try
            {
                wait.Until(el => LocatorWaitHelper.SafeDisplayed(el) && LocatorWaitHelper.SafeEnabled(el));
            }
            catch (WebDriverTimeoutException)
            {
                throw new WebDriverTimeoutException(
                    $"Element was not clickable after {timeoutSeconds}s.");
            }

            return element;
        }

        #endregion
    }
}
