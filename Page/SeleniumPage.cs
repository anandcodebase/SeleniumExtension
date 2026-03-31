using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SimpleSeleniumSupport.Selectors;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace SimpleSeleniumSupport.Page
{
    /// <summary>
    /// Playwright-style page API wrapping an <see cref="IWebDriver"/> with higher-level
    /// navigation, wait, JavaScript evaluation, screenshot, cookie, storage, and dialog APIs.
    /// Obtain via <c>driver.AsPage()</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// var page = driver.AsPage();
    /// page.GoTo("https://example.com");
    /// page.WaitForLoadState(LoadState.NetworkIdle);
    /// page.WaitForUrl("**/dashboard");
    ///
    /// string title = page.Evaluate&lt;string&gt;("document.title");
    /// page.Screenshot("screenshots/landing.png");
    ///
    /// page.SetLocalStorageItem("theme", "dark");
    /// page.AddCookie("session", "abc123");
    ///
    /// page.OnDialog(dialog => dialog.Accept());
    /// </code>
    /// </example>
    public sealed class SeleniumPage
    {
        private readonly IWebDriver _driver;
        private Action<SeleniumDialog>? _dialogHandler;
        private Thread? _dialogWatcher;
        private volatile bool _watchingDialogs;
        private string _downloadDirectory = Path.Combine(Path.GetTempPath(), "SSS_Downloads");

        internal SeleniumPage(IWebDriver driver)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
        }

        /// <summary>The underlying <see cref="IWebDriver"/>.</summary>
        public IWebDriver Driver => _driver;

        /// <summary>Current page URL.</summary>
        public string Url => _driver.Url;

        /// <summary>Current page title.</summary>
        public string Title => _driver.Title;

        /// <summary>Current page source HTML.</summary>
        public string Content() => _driver.PageSource;

        // ─── Navigation ───────────────────────────────────────────────────────────

        /// <summary>Navigates to <paramref name="url"/>.</summary>
        public SeleniumPage GoTo(string url)
        {
            _driver.Navigate().GoToUrl(url);
            return this;
        }

        /// <summary>Reloads the current page.</summary>
        public SeleniumPage Reload()
        {
            _driver.Navigate().Refresh();
            return this;
        }

        /// <summary>Goes back in the browser history.</summary>
        public SeleniumPage GoBack()
        {
            _driver.Navigate().Back();
            return this;
        }

        /// <summary>Goes forward in the browser history.</summary>
        public SeleniumPage GoForward()
        {
            _driver.Navigate().Forward();
            return this;
        }

        // ─── Wait helpers ─────────────────────────────────────────────────────────

        /// <summary>
        /// Waits until the page URL matches the glob <paramref name="urlPattern"/> (e.g. <c>**/dashboard</c>).
        /// Supports raw regex patterns starting with <c>^</c>.
        /// </summary>
        public SeleniumPage WaitForUrl(string urlPattern, int timeoutSeconds = -1)
        {
            if (timeoutSeconds <= 0) timeoutSeconds = SimpleSeleniumSupportDefaults.WaitTimeoutSeconds;
            var regex = Network.RoutePattern.ToRegex(urlPattern);
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSeconds));
            wait.Until(_ => regex.IsMatch(_driver.Url ?? ""));
            return this;
        }

        /// <summary>
        /// Waits until the page reaches the specified <paramref name="state"/>.
        /// </summary>
        public SeleniumPage WaitForLoadState(LoadState state = LoadState.Load, int timeoutSeconds = -1)
        {
            if (timeoutSeconds <= 0) timeoutSeconds = SimpleSeleniumSupportDefaults.WaitTimeoutSeconds;
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

            switch (state)
            {
                case LoadState.DomContentLoaded:
                    WaitForReadyState("interactive", deadline);
                    break;
                case LoadState.Load:
                    WaitForReadyState("complete", deadline);
                    break;
                case LoadState.NetworkIdle:
                    WaitForReadyState("complete", deadline);
                    // Additional quiet period — simple approximation
                    Thread.Sleep(500);
                    break;
            }
            return this;
        }

        /// <summary>Waits until a CSS selector matches at least one element.</summary>
        public SeleniumPage WaitForSelector(string selector, int timeoutSeconds = -1)
        {
            if (timeoutSeconds <= 0) timeoutSeconds = SimpleSeleniumSupportDefaults.WaitTimeoutSeconds;
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSeconds));
            wait.IgnoreExceptionTypes(typeof(NoSuchElementException), typeof(StaleElementReferenceException));
            bool isXPath = selector.TrimStart().StartsWith("/");
            var by = isXPath ? By.XPath(selector) : By.CssSelector(selector);
            wait.Until(d => d.FindElements(by).Count > 0);
            return this;
        }

        /// <summary>
        /// Waits until <paramref name="jsFunction"/> (a zero-argument JS function body) returns truthy.
        /// </summary>
        public SeleniumPage WaitForFunction(string jsFunction, int timeoutSeconds = -1)
        {
            if (timeoutSeconds <= 0) timeoutSeconds = SimpleSeleniumSupportDefaults.WaitTimeoutSeconds;
            var wait = new WebDriverWait(_driver, TimeSpan.FromSeconds(timeoutSeconds));
            var jsExecutor = (IJavaScriptExecutor)_driver;
            wait.Until(_ =>
            {
                try
                {
                    var result = jsExecutor.ExecuteScript($"return (function(){{ {jsFunction} }})();");
                    return result is bool b ? b : result != null;
                }
                catch { return false; }
            });
            return this;
        }

        // ─── JavaScript evaluation ────────────────────────────────────────────────

        /// <summary>
        /// Executes <paramref name="script"/> in the page context and returns the result
        /// cast to <typeparamref name="T"/>.
        /// </summary>
        public T? Evaluate<T>(string script)
        {
            var raw = ((IJavaScriptExecutor)_driver).ExecuteScript(script);
            if (raw is T t) return t;
            try { return (T)Convert.ChangeType(raw, typeof(T))!; }
            catch { return default; }
        }

        /// <summary>Executes <paramref name="script"/> with no return value.</summary>
        public void Evaluate(string script) =>
            ((IJavaScriptExecutor)_driver).ExecuteScript(script);

        // ─── Page content ─────────────────────────────────────────────────────────

        /// <summary>Replaces the entire page DOM with <paramref name="html"/>.</summary>
        public SeleniumPage SetContent(string html)
        {
            var escaped = html.Replace("\\", "\\\\").Replace("`", "\\`");
            ((IJavaScriptExecutor)_driver)
                .ExecuteScript($"document.open(); document.write(`{escaped}`); document.close();");
            return this;
        }

        // ─── Screenshot ───────────────────────────────────────────────────────────

        /// <summary>Takes a screenshot. Optionally saves to <paramref name="path"/>.</summary>
        public byte[] Screenshot(string? path = null, bool fullPage = false)
        {
            if (fullPage)
            {
                // Scroll to top, expand viewport height via JS, then screenshot
                var jsExecutor = (IJavaScriptExecutor)_driver;
                jsExecutor.ExecuteScript("window.scrollTo(0, 0);");
            }

            var screenshot = ((ITakesScreenshot)_driver).GetScreenshot();
            if (path != null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
                screenshot.SaveAsFile(path);
            }
            return screenshot.AsByteArray;
        }

        // ─── Cookies ─────────────────────────────────────────────────────────────

        /// <summary>Adds a cookie with the given <paramref name="name"/> and <paramref name="value"/>.</summary>
        public SeleniumPage AddCookie(string name, string value, string? domain = null, string? path = null)
        {
            var cookie = domain != null
                ? new Cookie(name, value, domain, path ?? "/", null)
                : new Cookie(name, value);
            _driver.Manage().Cookies.AddCookie(cookie);
            return this;
        }

        /// <summary>Returns all cookies for the current page.</summary>
        public ReadOnlyCollection<Cookie> GetCookies() => _driver.Manage().Cookies.AllCookies;

        /// <summary>Deletes all cookies.</summary>
        public SeleniumPage ClearCookies()
        {
            _driver.Manage().Cookies.DeleteAllCookies();
            return this;
        }

        /// <summary>Deletes the cookie with the given <paramref name="name"/>.</summary>
        public SeleniumPage DeleteCookie(string name)
        {
            _driver.Manage().Cookies.DeleteCookieNamed(name);
            return this;
        }

        // ─── Storage ─────────────────────────────────────────────────────────────

        /// <summary>Sets a <c>localStorage</c> key.</summary>
        public SeleniumPage SetLocalStorageItem(string key, string value)
        {
            ((IJavaScriptExecutor)_driver)
                .ExecuteScript($"localStorage.setItem(arguments[0], arguments[1]);", key, value);
            return this;
        }

        /// <summary>Gets a <c>localStorage</c> value by key.</summary>
        public string? GetLocalStorageItem(string key) =>
            ((IJavaScriptExecutor)_driver)
                .ExecuteScript("return localStorage.getItem(arguments[0]);", key) as string;

        /// <summary>Removes a <c>localStorage</c> key.</summary>
        public SeleniumPage RemoveLocalStorageItem(string key)
        {
            ((IJavaScriptExecutor)_driver)
                .ExecuteScript("localStorage.removeItem(arguments[0]);", key);
            return this;
        }

        /// <summary>Clears all <c>localStorage</c>.</summary>
        public SeleniumPage ClearLocalStorage()
        {
            ((IJavaScriptExecutor)_driver).ExecuteScript("localStorage.clear();");
            return this;
        }

        /// <summary>Sets a <c>sessionStorage</c> key.</summary>
        public SeleniumPage SetSessionStorageItem(string key, string value)
        {
            ((IJavaScriptExecutor)_driver)
                .ExecuteScript("sessionStorage.setItem(arguments[0], arguments[1]);", key, value);
            return this;
        }

        /// <summary>Gets a <c>sessionStorage</c> value by key.</summary>
        public string? GetSessionStorageItem(string key) =>
            ((IJavaScriptExecutor)_driver)
                .ExecuteScript("return sessionStorage.getItem(arguments[0]);", key) as string;

        // ─── Locator factories ────────────────────────────────────────────────────

        /// <summary>Creates a <see cref="Locator"/> from a CSS/XPath selector.</summary>
        public Locator Locator(string selector) => _driver.Locator(selector);

        /// <summary>Creates a <see cref="Locator"/> by ARIA role.</summary>
        public Locator GetByRole(string role, string? accessibleName = null, LocatorOptions? options = null) =>
            _driver.LocatorByRole(role, accessibleName, options);

        /// <summary>Creates a <see cref="Locator"/> by text content.</summary>
        public Locator GetByText(string text, LocatorOptions? options = null) =>
            _driver.LocatorByText(text, options);

        /// <summary>Creates a <see cref="Locator"/> by test ID.</summary>
        public Locator GetByTestId(string testId, LocatorOptions? options = null) =>
            _driver.LocatorByTestId(testId, options);

        /// <summary>Creates a <see cref="FrameLocator"/> for an iframe.</summary>
        public FrameLocator FrameLocator(string frameSelector) => _driver.FrameLocator(frameSelector);

        // ─── Dialogs ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Registers a handler that is called when a browser dialog (alert/confirm/prompt) appears.
        /// The handler runs on a background thread. If no handler is registered the dialog is
        /// auto-dismissed. Call with <c>null</c> to remove the handler.
        /// </summary>
        public SeleniumPage OnDialog(Action<SeleniumDialog>? handler)
        {
            _dialogHandler = handler;
            if (handler != null && !_watchingDialogs)
                StartDialogWatcher();
            else if (handler == null)
                StopDialogWatcher();
            return this;
        }

        /// <summary>
        /// Waits up to <paramref name="timeoutMs"/> for a dialog to appear, then returns it.
        /// The caller is responsible for calling <c>Accept()</c> or <c>Dismiss()</c>.
        /// </summary>
        public SeleniumDialog WaitForDialog(int timeoutMs = 5000)
        {
            var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var alert = _driver.SwitchTo().Alert();
                    return new SeleniumDialog(alert, DialogType.Unknown);
                }
                catch (NoAlertPresentException) { Thread.Sleep(100); }
            }
            throw new TimeoutException($"No dialog appeared within {timeoutMs}ms.");
        }

        // ─── Downloads ────────────────────────────────────────────────────────────

        /// <summary>
        /// Sets the directory where downloads are expected to land.
        /// Default: a temp folder under <c>%TEMP%/SSS_Downloads</c>.
        /// </summary>
        public SeleniumPage SetDownloadDirectory(string directory)
        {
            _downloadDirectory = directory;
            return this;
        }

        /// <summary>
        /// Runs <paramref name="action"/> (which should trigger a download) and waits for a new
        /// file to appear in the download directory.  Returns a <see cref="DownloadInfo"/>
        /// describing the downloaded file.
        /// </summary>
        /// <param name="action">The action that triggers the download (e.g. clicking a button).</param>
        /// <param name="timeoutMs">How long to wait for the file to appear.</param>
        public DownloadInfo ExpectDownload(Action action, int timeoutMs = 30_000)
        {
            Directory.CreateDirectory(_downloadDirectory);
            using var watcher = new DownloadWatcher(_downloadDirectory);
            action();
            return watcher.WaitForDownload(timeoutMs);
        }

        // ─── Viewport / window ────────────────────────────────────────────────────

        /// <summary>Sets the browser viewport to <paramref name="width"/> × <paramref name="height"/> pixels.</summary>
        public SeleniumPage SetViewportSize(int width, int height)
        {
            _driver.Manage().Window.Size = new System.Drawing.Size(width, height);
            return this;
        }

        /// <summary>Returns the current viewport (window) size.</summary>
        public System.Drawing.Size GetViewportSize() => _driver.Manage().Window.Size;

        // ─── Geolocation (JS override) ────────────────────────────────────────────

        /// <summary>
        /// Overrides the browser's Geolocation API to return the given coordinates.
        /// Only works on pages that call <c>navigator.geolocation.getCurrentPosition()</c>
        /// <em>after</em> this method is called.
        /// </summary>
        public SeleniumPage SetGeolocation(double latitude, double longitude, double accuracy = 1.0)
        {
            ((IJavaScriptExecutor)_driver).ExecuteScript($@"
                Object.defineProperty(navigator.geolocation, 'getCurrentPosition', {{
                    value: function(success) {{
                        success({{ coords: {{ latitude: {latitude}, longitude: {longitude}, accuracy: {accuracy} }} }});
                    }},
                    configurable: true
                }});");
            return this;
        }

        // ─── Emulation ────────────────────────────────────────────────────────────

        /// <summary>Injects <c>navigator.userAgent</c> override via JS.</summary>
        public SeleniumPage SetUserAgent(string userAgent)
        {
            ((IJavaScriptExecutor)_driver).ExecuteScript(
                "Object.defineProperty(navigator, 'userAgent', {get: () => arguments[0], configurable: true});",
                userAgent);
            return this;
        }

        // ─── Helpers ─────────────────────────────────────────────────────────────

        private void WaitForReadyState(string state, DateTime deadline)
        {
            var jsExecutor = (IJavaScriptExecutor)_driver;
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var readyState = jsExecutor.ExecuteScript("return document.readyState") as string;
                    if (state == "interactive" && (readyState == "interactive" || readyState == "complete"))
                        return;
                    if (state == "complete" && readyState == "complete")
                        return;
                }
                catch { /* driver may be navigating */ }
                Thread.Sleep(100);
            }
            throw new WebDriverTimeoutException($"Page did not reach readyState='{state}' in time.");
        }

        private void StartDialogWatcher()
        {
            _watchingDialogs = true;
            _dialogWatcher = new Thread(() =>
            {
                while (_watchingDialogs)
                {
                    try
                    {
                        var alert = _driver.SwitchTo().Alert();
                        var dialog = new SeleniumDialog(alert, DialogType.Unknown);
                        try { _dialogHandler?.Invoke(dialog); }
                        catch { /* handler threw — dismiss the dialog to avoid hanging */ }
                        try { alert.Dismiss(); } catch { /* already handled */ }
                    }
                    catch (NoAlertPresentException) { Thread.Sleep(150); }
                    catch { Thread.Sleep(150); }
                }
            })
            { IsBackground = true, Name = "SSS-DialogWatcher" };
            _dialogWatcher.Start();
        }

        private void StopDialogWatcher()
        {
            _watchingDialogs = false;
            _dialogWatcher = null;
        }
    }
}
