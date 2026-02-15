using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSeleniumSupport.Selectors
{
    /// <summary>
    /// Shared wait/polling helpers for locator operations.
    /// Works with both IWebDriver and IWebElement via ISearchContext.
    /// </summary>
    internal static class LocatorWaitHelper
    {
        internal static LocatorOptions DefaultOptions() => new LocatorOptions();

        internal static IWebElement FindElementWithWait(
            ISearchContext context, By by, LocatorOptions options, bool throwOnTimeout = true)
        {
            options ??= DefaultOptions();

            if (options.Wait == WaitUntil.None)
            {
                try { return context.FindElement(by); }
                catch { if (throwOnTimeout) throw; return null; }
            }

            var wait = new DefaultWait<ISearchContext>(context)
            {
                Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds)),
                PollingInterval = TimeSpan.FromMilliseconds(Math.Max(50, options.PollingMs))
            };
            wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));

            try
            {
                return wait.Until(ctx =>
                {
                    try
                    {
                        var e = ctx.FindElement(by);
                        return EvaluateWaitCondition(e, options) ? e : null;
                    }
                    catch { return null; }
                });
            }
            catch (WebDriverTimeoutException)
            {
                if (throwOnTimeout)
                    throw new WebDriverTimeoutException(
                        $"Timed out after {options.TimeoutSeconds}s waiting for '{by}' ({options.Wait})");
                return null;
            }
        }

        internal static IReadOnlyCollection<IWebElement> FindElementsWithWait(
            ISearchContext context, By by, LocatorOptions options, bool throwOnTimeout = true)
        {
            options ??= DefaultOptions();

            if (options.Wait == WaitUntil.None)
            {
                var dd = context.FindElements(by);
                return dd == null ? Array.Empty<IWebElement>() : dd.ToList().AsReadOnly();
            }

            var wait = new DefaultWait<ISearchContext>(context)
            {
                Timeout = TimeSpan.FromSeconds(Math.Max(1, options.TimeoutSeconds)),
                PollingInterval = TimeSpan.FromMilliseconds(Math.Max(50, options.PollingMs))
            };
            wait.IgnoreExceptionTypes(typeof(StaleElementReferenceException));

            try
            {
                return wait.Until(ctx =>
                {
                    try
                    {
                        var list = ctx.FindElements(by);
                        if (list == null || list.Count == 0) return null;
                        if (options.Wait == WaitUntil.Exists) return list.ToList().AsReadOnly();
                        var ok = list.Where(el => EvaluateWaitCondition(el, options)).ToList();
                        return ok.Count > 0 ? ok.AsReadOnly() : null;
                    }
                    catch { return null; }
                });
            }
            catch (WebDriverTimeoutException)
            {
                if (throwOnTimeout)
                    throw new WebDriverTimeoutException(
                        $"Timed out after {options.TimeoutSeconds}s waiting for elements '{by}' ({options.Wait})");
                return Array.Empty<IWebElement>();
            }
        }

        internal static bool EvaluateWaitCondition(IWebElement el, LocatorOptions options)
        {
            switch (options.Wait)
            {
                case WaitUntil.Exists: return true;
                case WaitUntil.Visible: return SafeDisplayed(el);
                case WaitUntil.Enabled: return SafeDisplayed(el) && SafeEnabled(el);
                case WaitUntil.Clickable: return SafeDisplayed(el) && SafeEnabled(el);
                default: return true;
            }
        }

        internal static bool SafeDisplayed(IWebElement el)
        {
            try { return el.Displayed; } catch { return false; }
        }

        internal static bool SafeEnabled(IWebElement el)
        {
            try { return el.Enabled; } catch { return false; }
        }
    }
}
