using OpenQA.Selenium;
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
    }
}
