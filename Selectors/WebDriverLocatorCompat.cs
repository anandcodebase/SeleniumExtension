using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System;
using System.Linq;
using SeleniumBy = OpenQA.Selenium.By;

namespace SimpleSeleniumSupport.Selectors
{
    /// <summary>
    /// Compatibility locator extensions:
    /// Provides GetByLabel, GetByPlaceholder, GetByAlt, GetByTitle and TryGet* overloads
    /// so older tests keep compiling while using the newer locator implementation.
    /// These are defensive and use WebDriverWait for the requested timeout.
    /// </summary>
    public static class WebDriverLocatorCompat
    {
        private const int DefaultTimeoutSeconds = 8;
        private const int PollMs = 200;

        #region GetByLabel / TryGetByLabel

        public static IWebElement GetByLabel(this IWebDriver driver, string labelText, int timeoutSeconds = DefaultTimeoutSeconds)
        {
            var found = driver.TryGetByLabel(labelText, out var el, timeoutSeconds);
            if (found) return el!;
            throw new NoSuchElementException($"Could not find element by label '{labelText}'.");
        }

        public static bool TryGetByLabel(this IWebDriver driver, string labelText, out IWebElement element, int timeoutSeconds = DefaultTimeoutSeconds)
        {
            element = null!;
            if (driver == null) return false;

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)))
            {
                PollingInterval = TimeSpan.FromMilliseconds(PollMs)
            };
            wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(NoSuchElementException));

            try
            {
                var result = wait.Until(drv =>
                {
                    try
                    {
                        // 1) find <label> whose text matches (normalize-space)
                        var labelXpath = $"//label[normalize-space(string(.)) = {Quote(labelText)}]";
                        var labels = drv.FindElements(SeleniumBy.XPath(labelXpath));
                        if (labels != null && labels.Count > 0)
                        {
                            var lab = labels.First();
                            var forAttr = lab.GetAttribute("for");
                            if (!string.IsNullOrEmpty(forAttr))
                            {
                                var target = drv.FindElements(SeleniumBy.Id(forAttr)).FirstOrDefault();
                                if (target != null) return target;
                            }
                            // try descendant controls
                            var nested = lab.FindElements(SeleniumBy.XPath(".//input|.//textarea|.//select"));
                            if (nested != null && nested.Count > 0) return nested.First();
                        }

                        // 2) aria-label match
                        var ariaXpath = $"//*[@aria-label = {Quote(labelText)} or normalize-space(@aria-label) = {Quote(labelText)}]";
                        var aria = drv.FindElements(SeleniumBy.XPath(ariaXpath)).FirstOrDefault();
                        if (aria != null) return aria;

                        // 3) aria-labelledby: find element with text then find elements referencing it
                        var labelledXpath = $"//*[normalize-space(string(.)) = {Quote(labelText)}]";
                        var labelElement = drv.FindElements(SeleniumBy.XPath(labelledXpath)).FirstOrDefault();
                        if (labelElement != null)
                        {
                            var id = labelElement.GetAttribute("id");
                            if (!string.IsNullOrEmpty(id))
                            {
                                var refEl = drv.FindElements(SeleniumBy.XPath($"//*[@aria-labelledby = {Quote(id)}]")).FirstOrDefault();
                                if (refEl != null) return refEl;
                            }
                        }

                        // 4) fallback: find input elements whose nearest preceding label text contains the labelText (simple heuristic)
                        var neighborXpath = $"//label[contains(normalize-space(string(.)), {QuotePartial(labelText)})]//input|//label[contains(normalize-space(string(.)), {QuotePartial(labelText)})]//textarea|//label[contains(normalize-space(string(.)), {QuotePartial(labelText)})]//select";
                        var neighbor = drv.FindElements(SeleniumBy.XPath(neighborXpath)).FirstOrDefault();
                        if (neighbor != null) return neighbor;

                        return null;
                    }
                    catch
                    {
                        return null;
                    }
                });

                if (result != null)
                {
                    element = result;
                    return true;
                }

                return false;
            }
            catch (WebDriverTimeoutException)
            {
                return false;
            }
        }

        #endregion

        #region GetByPlaceholder / TryGetByPlaceholder

        public static IWebElement GetByPlaceholder(this IWebDriver driver, string placeholder, int timeoutSeconds = DefaultTimeoutSeconds)
        {
            var found = driver.TryGetByPlaceholder(placeholder, out var el, timeoutSeconds);
            if (found) return el!;
            throw new NoSuchElementException($"Could not find element by placeholder '{placeholder}'.");
        }

        public static bool TryGetByPlaceholder(this IWebDriver driver, string placeholder, out IWebElement element, int timeoutSeconds = DefaultTimeoutSeconds)
        {
            element = null!;
            if (driver == null) return false;
            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)))
            {
                PollingInterval = TimeSpan.FromMilliseconds(PollMs)
            };
            wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(NoSuchElementException));

            try
            {
                var xpath = $"//*[@placeholder = {Quote(placeholder)} or contains(translate(@placeholder,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'), {QuotePartial(placeholder)})]";
                var elFound = wait.Until(drv =>
                {
                    try
                    {
                        var e = drv.FindElements(SeleniumBy.XPath(xpath));
                        return (e != null && e.Count > 0) ? e.First() : null;
                    }
                    catch { return null; }
                });

                if (elFound != null)
                {
                    element = elFound;
                    return true;
                }

                return false;
            }
            catch (WebDriverTimeoutException)
            {
                return false;
            }
        }

        #endregion

        #region GetByAlt / TryGetByAlt

        public static IWebElement GetByAlt(this IWebDriver driver, string altText, int timeoutSeconds = DefaultTimeoutSeconds)
        {
            var found = driver.TryGetByAlt(altText, out var el, timeoutSeconds);
            if (found) return el!;
            throw new NoSuchElementException($"Could not find element by alt='{altText}'.");
        }

        public static bool TryGetByAlt(this IWebDriver driver, string altText, out IWebElement element, int timeoutSeconds = DefaultTimeoutSeconds)
        {
            element = null!;
            if (driver == null) return false;

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)))
            {
                PollingInterval = TimeSpan.FromMilliseconds(PollMs)
            };
            wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(NoSuchElementException));

            try
            {
                var xpath = $"//*[@alt = {Quote(altText)} or contains(translate(@alt,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'), {QuotePartial(altText)})]";
                var foundEl = wait.Until(drv =>
                {
                    try
                    {
                        var e = drv.FindElements(SeleniumBy.XPath(xpath));
                        return (e != null && e.Count > 0) ? e.First() : null;
                    }
                    catch { return null; }
                });

                if (foundEl != null)
                {
                    element = foundEl;
                    return true;
                }
                return false;
            }
            catch (WebDriverTimeoutException)
            {
                return false;
            }
        }

        #endregion

        #region GetByTitle / TryGetByTitle

        public static IWebElement GetByTitle(this IWebDriver driver, string title, int timeoutSeconds = DefaultTimeoutSeconds)
        {
            var found = driver.TryGetByTitle(title, out var el, timeoutSeconds);
            if (found) return el!;
            throw new NoSuchElementException($"Could not find element by title='{title}'.");
        }

        public static bool TryGetByTitle(this IWebDriver driver, string title, out IWebElement element, int timeoutSeconds = DefaultTimeoutSeconds)
        {
            element = null!;
            if (driver == null) return false;

            var wait = new WebDriverWait(driver, TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)))
            {
                PollingInterval = TimeSpan.FromMilliseconds(PollMs)
            };
            wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException), typeof(NoSuchElementException));

            try
            {
                var xpath = $"//*[@title = {Quote(title)} or contains(translate(@title,'ABCDEFGHIJKLMNOPQRSTUVWXYZ','abcdefghijklmnopqrstuvwxyz'), {QuotePartial(title)})]";
                var foundEl = wait.Until(drv =>
                {
                    try
                    {
                        var e = drv.FindElements(SeleniumBy.XPath(xpath));
                        return (e != null && e.Count > 0) ? e.First() : null;
                    }
                    catch { return null; }
                });

                if (foundEl != null)
                {
                    element = foundEl;
                    return true;
                }
                return false;
            }
            catch (WebDriverTimeoutException)
            {
                return false;
            }
        }

        #endregion

        #region Utilities

        private static string Quote(string s) => XPathHelper.Quote(s);

        private static string QuotePartial(string s) => XPathHelper.QuotePartial(s);

        #endregion
    }
}
