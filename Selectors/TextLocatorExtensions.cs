using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSeleniumSupport.Selectors
{
    public static class TextLocatorExtensions
    {
        /// <summary>
        /// Returns the first matching element whose own text (not descendant-only text) matches the given text.
        /// Uses LocatorOptions for exact/contains and case sensitivity behavior.
        /// </summary>
        public static IWebElement GetByText(this IWebDriver driver, string text, LocatorOptions? options = null)
        {
            options ??= new LocatorOptions();
            var list = FindElementsByTextDirectMatch(driver, text, options);
            if (list == null || list.Count == 0)
                throw new NoSuchElementException($"No element found with text '{text}'");
            return list.First();
        }

        /// <summary>
        /// Try-get variant: returns null if not found.
        /// </summary>
        public static IWebElement? TryGetByText(this IWebDriver driver, string text, LocatorOptions? options = null)
        {
            options ??= new LocatorOptions();
            var list = FindElementsByTextDirectMatch(driver, text, options);
            return list.FirstOrDefault();
        }

        /// <summary>
        /// Returns all matching elements whose own text matches the provided text.
        /// </summary>
        public static IReadOnlyCollection<IWebElement> GetAllByText(this IWebDriver driver, string text, LocatorOptions? options = null)
        {
            options ??= new LocatorOptions();
            var list = FindElementsByTextDirectMatch(driver, text, options);
            if (list == null || list.Count == 0)
                throw new NoSuchElementException($"No elements found with text '{text}'");
            return list;
        }

        /// <summary>
        /// Try-get-all variant: returns empty collection if none found.
        /// </summary>
        public static IReadOnlyCollection<IWebElement> TryGetAllByText(this IWebDriver driver, string text, LocatorOptions? options = null)
        {
            options ??= new LocatorOptions();
            return FindElementsByTextDirectMatch(driver, text, options);
        }

        /// <summary>
        /// Core routine: finds elements whose direct text nodes match (exact or contains),
        /// filters out large containers that contain matching text only in descendants.
        /// </summary>
        private static IReadOnlyCollection<IWebElement> FindElementsByTextDirectMatch(IWebDriver driver, string text, LocatorOptions options)
        {
            if (driver == null) return Array.Empty<IWebElement>();
            if (text == null) text = "";

            // Build an XPath that matches elements that have at least one direct text node matching the pattern.
            // exact: text node exactly equals normalized text
            // contains: any direct text node contains the needle (case-insensitive by default)
            string xpath;
            if (options.ExactMatch)
            {
                xpath = $"//*[normalize-space(text()) = {QuoteForXPath(text)}]";
            }
            else
            {
                if (options.CaseSensitive)
                {
                    xpath = $"//*[text()[contains(normalize-space(.), {QuoteForXPath(text)})]]";
                }
                else
                {
                    // lower-case comparison via translate on the text node content
                    var lowerNeedle = text.ToLowerInvariant();
                    xpath = $"//*[text()[contains(translate(normalize-space(.), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), {QuoteForXPath(lowerNeedle)}]]";
                }
            }

            // Collect candidates
            IReadOnlyCollection<IWebElement> candidates;
            try
            {
                var found = driver.FindElements(By.XPath(xpath)).ToList();
                // Filter to prefer the most specific elements:
                var filtered = found.Where(el => IsBestTextMatch(el, text, options)).ToList();
                candidates = filtered.AsReadOnly();
            }
            catch
            {
                candidates = Array.Empty<IWebElement>();
            }

            return candidates;
        }

        /// <summary>
        /// Checks whether the element is a "best" match:
        /// - element has visible/non-empty text
        /// - none of its element children themselves contain the same matching text (so we avoid returning &lt;body&gt;)
        /// </summary>
        private static bool IsBestTextMatch(IWebElement element, string needle, LocatorOptions options)
        {
            try
            {
                if (element == null) return false;

                // Skip elements that are not displayed
                try { if (!element.Displayed) return false; } catch { /* ignore */ }

                var content = (element.Text ?? "").Trim();
                if (string.IsNullOrEmpty(content)) return false;

                // If any descendant element has text that also matches the needle, then prefer the descendant instead of this element.
                // We look for descendant elements whose normalized text contains the needle (respecting case setting).
                string descendantXpath;
                if (options.CaseSensitive)
                {
                    descendantXpath = $".//*[contains(normalize-space(string(.)), {QuoteForXPath(needle)})]";
                }
                else
                {
                    descendantXpath = $".//*[contains(translate(normalize-space(string(.)),'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'), {QuoteForXPath(needle.ToLowerInvariant())})]";
                }

                var childrenWithMatch = element.FindElements(By.XPath(descendantXpath));
                if (childrenWithMatch != null && childrenWithMatch.Count > 0)
                {
                    // If one of the children is actually the same as element (rare), allow; otherwise skip element
                    // (we'll return the children via GetAllByText)
                    return false;
                }

                // Now verify this element actually matches the needle itself (exact or contains)
                if (options.ExactMatch)
                {
                    return string.Equals(content, needle, options.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
                }
                else
                {
                    return content.IndexOf(needle, options.CaseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase) >= 0;
                }
            }
            catch
            {
                return false;
            }
        }

        private static string QuoteForXPath(string value) => XPathHelper.Quote(value);
    }
}
