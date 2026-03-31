using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSeleniumSupport.Selectors
{
    /// <summary>
    /// Playwright-style locator scoped to the contents of an iframe.
    /// <para>
    /// Before each element search, the driver is switched into the target frame.
    /// After the operation completes (or throws), the driver is automatically switched
    /// back to the default content.  Nested frames are supported by chaining
    /// <see cref="FrameLocator(string)"/> calls.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// // Single iframe
    /// driver.FrameLocator("#checkout-frame").LocatorByRole("button", "Pay").Click();
    ///
    /// // Nested iframes
    /// driver.FrameLocator("#outer").FrameLocator("#inner").Locator("input[name=search]").Fill("hello");
    /// </code>
    /// </example>
    public sealed class FrameLocator
    {
        private readonly IWebDriver _driver;
        private readonly Locator? _parentLocator; // if null, resolve from top-level driver
        private readonly string _frameSelector;

        /// <summary>
        /// Creates a <see cref="FrameLocator"/> targeting the frame matched by
        /// <paramref name="frameSelector"/> within the given <paramref name="parentLocator"/> scope,
        /// or directly from <paramref name="driver"/> when <paramref name="parentLocator"/> is <c>null</c>.
        /// </summary>
        internal FrameLocator(IWebDriver driver, Locator? parentLocator, string frameSelector)
        {
            _driver = driver ?? throw new ArgumentNullException(nameof(driver));
            _parentLocator = parentLocator;
            _frameSelector = frameSelector ?? throw new ArgumentNullException(nameof(frameSelector));
        }

        // ─── Factory methods (return Locator-within-frame) ─────────────────────────

        /// <summary>Returns a <see cref="Locator"/> scoped to <paramref name="selector"/> within this frame.</summary>
        public Locator Locator(string selector)
        {
            return CreateLocator(selector,
                ctx =>
                {
                    bool isXPath = selector.TrimStart().StartsWith("/") || selector.TrimStart().StartsWith("(");
                    var by = isXPath ? By.XPath(selector) : By.CssSelector(selector);
                    return ctx.FindElements(by).ToList().AsReadOnly();
                });
        }

        /// <summary>Returns a <see cref="Locator"/> scoped to the given ARIA role within this frame.</summary>
        public Locator GetByRole(string role, string? accessibleName = null, LocatorOptions? options = null)
        {
            options ??= new LocatorOptions();
            var by = new ByRole(role, accessibleName, options);
            return CreateLocator($"role={role}", ctx => ctx.FindElements(by).ToList().AsReadOnly());
        }

        /// <summary>Returns a <see cref="Locator"/> scoped to the given text within this frame.</summary>
        public Locator GetByText(string text, LocatorOptions? options = null)
        {
            options ??= new LocatorOptions();
            var by = new ByText(text, options);
            return CreateLocator($"text='{text}'", ctx => ctx.FindElements(by).ToList().AsReadOnly());
        }

        /// <summary>Returns a <see cref="Locator"/> scoped to the given test ID within this frame.</summary>
        public Locator GetByTestId(string testId, LocatorOptions? options = null)
        {
            options ??= new LocatorOptions();
            var by = new ByTestId(testId, options);
            return CreateLocator($"testid='{testId}'", ctx => ctx.FindElements(by).ToList().AsReadOnly());
        }

        /// <summary>Returns a nested <see cref="FrameLocator"/> scoped to an inner iframe.</summary>
        public FrameLocator InnerFrame(string innerFrameSelector)
        {
            // The outer frame becomes a Locator that switches into our frame first
            var outerLocatorInFrame = CreateLocator(_frameSelector,
                ctx =>
                {
                    var by = By.CssSelector(innerFrameSelector);
                    return ctx.FindElements(by).ToList().AsReadOnly();
                });
            return new FrameLocator(_driver, outerLocatorInFrame, innerFrameSelector);
        }

        // ─── Internal ─────────────────────────────────────────────────────────────

        private Locator CreateLocator(string description, Func<ISearchContext, IReadOnlyList<IWebElement>> resolver)
        {
            var self = this;
            return new Locator(_driver, null, _ =>
            {
                // 1. Always start from DefaultContent so re-entry across ResolveOne retries works cleanly
                try { _driver.SwitchTo().DefaultContent(); } catch { /* ignore */ }
                // 2. Switch into the target frame
                self.SwitchIntoFrame();
                // 3. Search within the now-current frame context.
                //    NOTE: We do NOT switch back here — the Locator's _frameRestoreAction handles that
                //    after the operation completes, so element properties can be accessed in-frame.
                return resolver(_driver);
            }, $"frame({self._frameSelector}) >> {description}",
            frameRestoreAction: () => { try { _driver.SwitchTo().DefaultContent(); } catch { /* ignore */ } });
        }

        private void SwitchIntoFrame()
        {
            // If there is a parent locator chain, switch into each ancestor frame first
            if (_parentLocator != null)
            {
                // The parent locator will have already handled switching when its resolver runs —
                // but since we own the frame switch, resolve the frame element manually.
                var frameElements = _parentLocator.ResolveAll();
                if (frameElements.Count == 0)
                    throw new NoSuchFrameException($"Parent frame locator resolved to no elements.");
                _driver.SwitchTo().Frame(frameElements[0]);
                return;
            }

            // No parent: switch by CSS selector or name/index from top-level
            bool isXPath = _frameSelector.TrimStart().StartsWith("/");
            IWebElement? frameEl = null;
            try
            {
                var by = isXPath ? By.XPath(_frameSelector) : By.CssSelector(_frameSelector);
                frameEl = _driver.FindElement(by);
            }
            catch { /* fall through to name/index attempt */ }

            if (frameEl != null)
            {
                _driver.SwitchTo().Frame(frameEl);
                return;
            }

            // Try by name or index (for simple selectors like "#0" or "myFrameName")
            if (int.TryParse(_frameSelector, out int frameIndex))
                _driver.SwitchTo().Frame(frameIndex);
            else
                _driver.SwitchTo().Frame(_frameSelector);
        }
    }
}
