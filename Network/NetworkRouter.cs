using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.DevTools;
using OpenQA.Selenium.Support.UI;
using SimpleSeleniumSupport.Logging;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SimpleSeleniumSupport.Network
{
    /// <summary>
    /// Playwright-style request routing / mocking for Selenium 4 (Chrome/Edge only).
    /// Wraps Selenium's <c>INetwork.AddRequestHandler</c> API to allow intercepting,
    /// mocking, or aborting HTTP requests.
    /// </summary>
    /// <example>
    /// <code>
    /// var router = new NetworkRouter(driver);
    /// router.StartRouting();
    ///
    /// // Mock an API endpoint
    /// router.Route("**/api/users", route =>
    ///     route.Fulfill(body: "[{\"id\":1}]", contentType: "application/json"));
    ///
    /// // Abort all image requests
    /// router.Route("**/*.png", route => route.Abort());
    ///
    /// // Wait for a request and run an action simultaneously
    /// var info = router.WaitForRequest("**/api/login", () =>
    ///     driver.FindElement(By.Id("submit")).Click());
    ///
    /// router.StopRouting();
    /// </code>
    /// </example>
    public sealed class NetworkRouter : IDisposable
    {
        private static readonly ILogger _log = LibraryLogger.For<NetworkRouter>();

        private readonly IWebDriver _driver;
        private INetwork? _network;
        private readonly List<RouteEntry> _routes = new();
        private bool _disposed;

        /// <summary>
        /// Initialises a new <see cref="NetworkRouter"/> bound to <paramref name="driver"/>.
        /// </summary>
        /// <exception cref="NotSupportedException">
        /// Thrown if <paramref name="driver"/> is not a Chrome/Edge DevTools driver.
        /// </exception>
        public NetworkRouter(IWebDriver driver)
        {
            if (driver is not IDevTools)
                throw new NotSupportedException(
                    "NetworkRouter requires a DevTools-capable driver (ChromeDriver or EdgeDriver). " +
                    "Firefox and Safari are not supported.");
            _driver = driver;
        }

        /// <summary>Activates network interception. Call once before registering routes.</summary>
        public void StartRouting()
        {
            if (_network != null)
            {
                _log.LogDebug("NetworkRouter is already active.");
                return;
            }

            _network = _driver.Manage().Network;
            _network.StartMonitoring().GetAwaiter().GetResult();
            _log.LogInformation("NetworkRouter started.");
        }

        /// <summary>Deactivates network interception and removes all registered routes.</summary>
        public void StopRouting()
        {
            if (_network == null) return;

            _routes.Clear();
            try { _network.ClearRequestHandlers(); } catch { /* ignore */ }

            _network.StopMonitoring().GetAwaiter().GetResult();
            _network = null;
            _log.LogInformation("NetworkRouter stopped.");
        }

        // ─── Route registration ───────────────────────────────────────────────────

        /// <summary>
        /// Registers a route handler for URLs matching <paramref name="urlPattern"/>.
        /// The handler is called synchronously on the intercepted request.
        /// First matching pattern wins (order of registration).
        /// </summary>
        /// <param name="urlPattern">
        /// Glob pattern (<c>**/api/**</c>) or raw regex starting with <c>^</c>.
        /// </param>
        /// <param name="handler">Action that receives a <see cref="Route"/> and decides how to handle it.</param>
        public void Route(string urlPattern, Action<Route> handler)
        {
            if (_network == null)
                throw new InvalidOperationException("Call StartRouting() before registering routes.");

            var entry = new RouteEntry(urlPattern, handler);
            _network.AddRequestHandler(entry.Handler);
            _routes.Add(entry);
            _log.LogDebug("Route registered: {Pattern}", urlPattern);
        }

        /// <summary>
        /// Removes a previously registered route by its URL pattern.
        /// Remaining routes are re-registered after clearing.
        /// </summary>
        public void RemoveRoute(string urlPattern)
        {
            if (_network == null) return;

            _routes.RemoveAll(r => r.Pattern == urlPattern);

            // Re-register remaining handlers after a full clear
            try { _network.ClearRequestHandlers(); } catch { /* ignore */ }
            foreach (var entry in _routes)
                _network.AddRequestHandler(entry.Handler);
        }

        // ─── WaitForRequest / WaitForResponse ─────────────────────────────────────

        /// <summary>
        /// Runs <paramref name="action"/> and waits until a request matching <paramref name="urlPattern"/>
        /// completes. Returns the matching <see cref="FullNetworkInfo"/>.
        /// </summary>
        /// <remarks>
        /// Requires a <see cref="Capture"/> to be active on the same driver so that
        /// request/response pairs are recorded. Creates a temporary one if needed.
        /// </remarks>
        public FullNetworkInfo WaitForRequest(
            string urlPattern,
            Action? action = null,
            int timeoutSeconds = -1)
        {
            if (timeoutSeconds <= 0) timeoutSeconds = SimpleSeleniumSupportDefaults.NetworkWaitTimeoutSeconds;

            using var capture = new Capture(_driver);
            capture.StartMonitoring(clearPrevious: true);

            action?.Invoke();

            var regex = RoutePattern.ToRegex(urlPattern);
            return capture.WaitForRequest(
                info => regex.IsMatch(info.RequestUrl ?? ""),
                timeoutSeconds);
        }

        /// <summary>
        /// Runs <paramref name="action"/> and waits until a response matching <paramref name="urlPattern"/>
        /// is received with a non-zero status code.
        /// </summary>
        public FullNetworkInfo WaitForResponse(
            string urlPattern,
            Action? action = null,
            int timeoutSeconds = -1)
        {
            if (timeoutSeconds <= 0) timeoutSeconds = SimpleSeleniumSupportDefaults.NetworkWaitTimeoutSeconds;

            using var capture = new Capture(_driver);
            capture.StartMonitoring(clearPrevious: true);

            action?.Invoke();

            var regex = RoutePattern.ToRegex(urlPattern);
            return capture.WaitForRequest(
                info => regex.IsMatch(info.RequestUrl ?? "") && info.ResponseStatusCode != 0,
                timeoutSeconds);
        }

        // ─── IDisposable ──────────────────────────────────────────────────────────

        /// <summary>Stops routing and releases resources.</summary>
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            if (_network != null) StopRouting();
        }
    }
}
