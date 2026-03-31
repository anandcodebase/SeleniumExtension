using OpenQA.Selenium;
using System;
using System.Collections.Generic;

namespace SimpleSeleniumSupport.Network
{
    /// <summary>
    /// Extension methods that attach Playwright-style request routing directly to an
    /// <see cref="IWebDriver"/> instance, creating and starting a <see cref="NetworkRouter"/>
    /// automatically.
    /// </summary>
    /// <example>
    /// <code>
    /// // Single-shot mock — router is started/stopped automatically
    /// using var router = driver.CreateRouter();
    /// router.Route("**/api/users", r => r.Fulfill(body: "[]", contentType: "application/json"));
    ///
    /// // Convenience one-liner (starts routing, returns running router)
    /// var router = driver.StartRouting();
    /// router.Route("**/*.png", r => r.Abort());
    /// ...
    /// router.Dispose(); // stops routing
    /// </code>
    /// </example>
    public static class NetworkRouterExtensions
    {
        /// <summary>
        /// Creates a new <see cref="NetworkRouter"/> bound to this driver and calls
        /// <see cref="NetworkRouter.StartRouting"/>. Dispose the returned router to stop.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// Thrown if the driver does not support DevTools (non-Chromium browsers).
        /// </exception>
        public static NetworkRouter StartRouting(this IWebDriver driver)
        {
            var router = new NetworkRouter(driver);
            router.StartRouting();
            return router;
        }

        /// <summary>
        /// Creates a <see cref="NetworkRouter"/> <em>without</em> starting it, so you can
        /// register routes before activation.
        /// </summary>
        public static NetworkRouter CreateRouter(this IWebDriver driver)
            => new NetworkRouter(driver);

        /// <summary>
        /// Waits for a request matching <paramref name="urlPattern"/> after running
        /// <paramref name="action"/>. Returns the matching <see cref="FullNetworkInfo"/>.
        /// </summary>
        public static FullNetworkInfo WaitForRequest(
            this IWebDriver driver,
            string urlPattern,
            Action action,
            int timeoutSeconds = -1)
        {
            using var router = new NetworkRouter(driver);
            router.StartRouting();
            return router.WaitForRequest(urlPattern, action, timeoutSeconds);
        }

        /// <summary>
        /// Waits for a response matching <paramref name="urlPattern"/> after running
        /// <paramref name="action"/>. Returns the matching <see cref="FullNetworkInfo"/>.
        /// </summary>
        public static FullNetworkInfo WaitForResponse(
            this IWebDriver driver,
            string urlPattern,
            Action action,
            int timeoutSeconds = -1)
        {
            using var router = new NetworkRouter(driver);
            router.StartRouting();
            return router.WaitForResponse(urlPattern, action, timeoutSeconds);
        }
    }
}
