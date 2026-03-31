using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using OpenQA.Selenium.Support.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;

namespace SimpleSeleniumSupport.Selectors
{
    /// <summary>
    /// A lazy, chainable, auto-waiting element locator — the Playwright-style counterpart to
    /// Selenium's eager <c>FindElement</c>.
    /// <para>
    /// A <see cref="Locator"/> describes <em>how</em> to find an element; the DOM is not queried
    /// until an action (Click, Fill, …) or state query (IsVisible, Count, …) is called.
    /// Every action re-evaluates the selector fresh, which means stale element references are
    /// recovered automatically.
    /// </para>
    /// </summary>
    /// <example>
    /// <code>
    /// // Basic usage
    /// driver.Locator("[data-testid='submit']").Click();
    ///
    /// // Chaining (scoped search)
    /// driver.Locator("form#checkout").GetByRole("button", "Pay Now").Click();
    ///
    /// // Filtering
    /// driver.Locator("li").Filter(hasText: "Alice").GetByRole("button").Click();
    ///
    /// // Nth / First / Last
    /// driver.Locator("tr").Nth(2).GetByRole("checkbox").Check();
    /// driver.Locator("td").Last.TextContent();
    ///
    /// // State queries (no throw)
    /// bool visible = driver.Locator("#banner").IsVisible();
    /// int count    = driver.Locator("li.item").Count();
    /// </code>
    /// </example>
    public sealed class Locator
    {
        private readonly IWebDriver _driver;
        private readonly Locator? _parent;
        private readonly Func<ISearchContext, IReadOnlyList<IWebElement>> _resolver;
        private readonly Func<IWebElement, bool>? _filter;
        private readonly string _description;
        // Invoked after each top-level operation to restore driver context (used by FrameLocator).
        private readonly Action? _frameRestoreAction;

        // ─── Constructors ─────────────────────────────────────────────────────────

        /// <summary>Creates a root Locator bound to a driver.</summary>
        internal Locator(
            IWebDriver driver,
            Func<ISearchContext, IReadOnlyList<IWebElement>> resolver,
            string description)
        {
            _driver = driver;
            _resolver = resolver;
            _description = description;
        }

        /// <summary>
        /// Creates a Locator with an optional parent scope, filter predicate, and frame-restore action.
        /// </summary>
        internal Locator(
            IWebDriver driver,
            Locator? parent,
            Func<ISearchContext, IReadOnlyList<IWebElement>> resolver,
            string description,
            Func<IWebElement, bool>? filter = null,
            Action? frameRestoreAction = null)
        {
            _driver = driver;
            _parent = parent;
            _resolver = resolver;
            _description = description;
            _filter = filter;
            _frameRestoreAction = frameRestoreAction;
        }

        // ─── Internal driver access ────────────────────────────────────────────────

        /// <summary>The underlying <see cref="IWebDriver"/> this locator is bound to.</summary>
        internal IWebDriver Driver => _driver;
        /// <summary>Human-readable description used in timeout messages.</summary>
        public string Description => _description;

        // ─── Chaining ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Narrows this locator to elements that satisfy the given criteria.
        /// </summary>
        /// <param name="hasText">Element must contain this text (case-insensitive substring).</param>
        /// <param name="has">Element must contain at least one element matching this locator.</param>
        /// <param name="hasNotText">Element must NOT contain this text.</param>
        /// <param name="hasNot">Element must NOT contain any element matching this locator.</param>
        public Locator Filter(
            string? hasText = null,
            Locator? has = null,
            string? hasNotText = null,
            Locator? hasNot = null)
        {
            Func<IWebElement, bool> predicate = element =>
            {
                if (hasText != null)
                {
                    var text = SafeText(element);
                    if (text == null || text.IndexOf(hasText, StringComparison.OrdinalIgnoreCase) < 0)
                        return false;
                }
                if (hasNotText != null)
                {
                    var text = SafeText(element);
                    if (text != null && text.IndexOf(hasNotText, StringComparison.OrdinalIgnoreCase) >= 0)
                        return false;
                }
                if (has != null)
                {
                    try
                    {
                        var found = has._resolver(element);
                        if (found.Count == 0) return false;
                    }
                    catch { return false; }
                }
                if (hasNot != null)
                {
                    try
                    {
                        var found = hasNot._resolver(element);
                        if (found.Count > 0) return false;
                    }
                    catch { /* treat as not found → pass */ }
                }
                return true;
            };

            var filteredDescription = _description;
            if (hasText != null) filteredDescription += $".filter(hasText='{hasText}')";
            if (hasNotText != null) filteredDescription += $".filter(hasNotText='{hasNotText}')";
            if (has != null) filteredDescription += ".filter(has=...)";
            if (hasNot != null) filteredDescription += ".filter(hasNot=...)";

            return new Locator(_driver, _parent, _resolver, filteredDescription, predicate);
        }

        /// <summary>Returns a Locator matching elements that satisfy BOTH this and <paramref name="other"/>.</summary>
        public Locator And(Locator other)
        {
            var self = this;
            return new Locator(_driver, null, _ =>
            {
                var thisElements = self.ResolveAll();
                var otherElements = other.ResolveAll();
                return thisElements.Where(e => otherElements.Any(oe => AreSameElement(e, oe, _driver))).ToList().AsReadOnly();
            }, $"({_description}) and ({other._description})");
        }

        /// <summary>Returns a Locator matching elements that satisfy EITHER this OR <paramref name="other"/>.</summary>
        public Locator Or(Locator other)
        {
            var self = this;
            return new Locator(_driver, null, _ =>
            {
                var thisElements = self.ResolveAll().ToList();
                var otherElements = other.ResolveAll();
                var result = new List<IWebElement>(thisElements);
                foreach (var element in otherElements)
                    if (!thisElements.Any(me => AreSameElement(me, element, _driver)))
                        result.Add(element);
                return result.AsReadOnly();
            }, $"({_description}) or ({other._description})");
        }

        /// <summary>
        /// Returns a Locator pointing to the element at zero-based <paramref name="index"/>.
        /// Negative values count from the end (-1 = last).
        /// </summary>
        public Locator Nth(int index)
        {
            var self = this;
            return new Locator(_driver, null, _ =>
            {
                var allElements = self.ResolveAll();
                var resolvedIndex = index < 0 ? allElements.Count + index : index;
                if (resolvedIndex < 0 || resolvedIndex >= allElements.Count) return Array.Empty<IWebElement>();
                return new ReadOnlyCollection<IWebElement>(new[] { allElements[resolvedIndex] });
            }, $"{_description}.nth({index})");
        }

        /// <summary>Locator pointing to the first matching element. Equivalent to <c>Nth(0)</c>.</summary>
        public Locator First => Nth(0);

        /// <summary>Locator pointing to the last matching element. Equivalent to <c>Nth(-1)</c>.</summary>
        public Locator Last => Nth(-1);

        // ─── Scoped GetBy* (return Locator for chaining) ─────────────────────────

        /// <summary>Returns a child Locator scoped to elements matching <paramref name="selector"/> within this one.</summary>
        public Locator Find(string selector)
        {
            bool isXPath = selector.TrimStart().StartsWith("/") || selector.TrimStart().StartsWith("(");
            By by = isXPath ? By.XPath(selector) : By.CssSelector(selector);
            return new Locator(_driver, this,
                ctx => ctx.FindElements(by).ToList().AsReadOnly(),
                $"{_description} >> {selector}");
        }

        /// <summary>Returns a child Locator scoped to role within this one.</summary>
        public Locator GetByRole(string role, string? accessibleName = null, LocatorOptions? options = null)
        {
            options ??= new LocatorOptions();
            var by = new ByRole(role, accessibleName, options);
            return new Locator(_driver, this,
                ctx => ctx.FindElements(by).ToList().AsReadOnly(),
                $"{_description} >> role={role}{(accessibleName != null ? $"[{accessibleName}]" : "")}");
        }

        /// <summary>Returns a child Locator scoped to text within this one.</summary>
        public Locator GetByText(string text, LocatorOptions? options = null)
        {
            options ??= new LocatorOptions();
            var by = new ByText(text, options);
            return new Locator(_driver, this,
                ctx => ctx.FindElements(by).ToList().AsReadOnly(),
                $"{_description} >> text='{text}'");
        }

        /// <summary>Returns a child Locator scoped to test ID within this one.</summary>
        public Locator GetByTestId(string testId, LocatorOptions? options = null)
        {
            options ??= new LocatorOptions();
            var by = new ByTestId(testId, options);
            return new Locator(_driver, this,
                ctx => ctx.FindElements(by).ToList().AsReadOnly(),
                $"{_description} >> testid='{testId}'");
        }

        /// <summary>Returns a <see cref="FrameLocator"/> scoped to an iframe within this locator.</summary>
        public FrameLocator InnerFrame(string frameSelector)
        {
            return new FrameLocator(_driver, this, frameSelector);
        }

        // ─── Resolution ────────────────────────────────────────────────────────────

        /// <summary>
        /// Resolves all currently matching elements. Does NOT wait — call <see cref="WaitFor"/> first
        /// if you need the DOM to catch up.
        /// </summary>
        internal IReadOnlyList<IWebElement> ResolveAll(ISearchContext? rootCtx = null)
        {
            ISearchContext ctx;
            if (_parent != null)
            {
                var parentEls = _parent.ResolveAll(rootCtx);
                if (parentEls.Count == 0) return Array.Empty<IWebElement>();
                ctx = parentEls[0];
            }
            else
            {
                ctx = rootCtx ?? (ISearchContext)_driver;
            }

            IReadOnlyList<IWebElement> elements;
            try { elements = _resolver(ctx); }
            catch (StaleElementReferenceException) { return Array.Empty<IWebElement>(); }
            catch { return Array.Empty<IWebElement>(); }

            if (_filter != null)
            {
                elements = elements.Where(e =>
                {
                    try { return _filter(e); }
                    catch { return false; }
                }).ToList().AsReadOnly();
            }

            return elements;
        }

        /// <summary>
        /// Resolves to a single element, polling until actionability is satisfied.
        /// Re-evaluates on every poll tick to survive stale references.
        /// </summary>
        internal IWebElement ResolveOne(ActionOptions? options = null, WaitUntil waitFor = WaitUntil.Visible)
        {
            options ??= new ActionOptions();
            var deadline = DateTime.UtcNow.AddSeconds(Math.Max(1, options.TimeoutSeconds));
            Exception? lastException = null;

            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    var elements = ResolveAll();
                    if (elements.Count == 0)
                    {
                        Thread.Sleep(options.PollingMs);
                        continue;
                    }

                    var element = elements[0];

                    if (!options.Force)
                    {
                        switch (waitFor)
                        {
                            case WaitUntil.Visible:
                                if (!LocatorWaitHelper.SafeDisplayed(element))
                                { Thread.Sleep(options.PollingMs); continue; }
                                break;
                            case WaitUntil.Enabled:
                            case WaitUntil.Clickable:
                                if (!LocatorWaitHelper.SafeDisplayed(element) || !LocatorWaitHelper.SafeEnabled(element))
                                { Thread.Sleep(options.PollingMs); continue; }
                                break;
                        }
                    }

                    return element;
                }
                catch (StaleElementReferenceException staleException)
                {
                    lastException = staleException;
                    Thread.Sleep(options.PollingMs);
                }
                catch (Exception exception)
                {
                    lastException = exception;
                    Thread.Sleep(options.PollingMs);
                }
            }

            throw new WebDriverTimeoutException(
                $"Timed out after {options.TimeoutSeconds}s waiting for '{_description}'", lastException);
        }

        // ─── Count / All ────────────────────────────────────────────────────────────

        /// <summary>Returns the number of elements currently matching. Returns 0 on error (never throws).</summary>
        public int Count()
        {
            try { return ResolveAll().Count; }
            catch { return 0; }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>
        /// Returns one <see cref="Locator"/> per currently-matched element.
        /// Stable at the moment of calling; use in a loop without re-querying.
        /// </summary>
        public List<Locator> All()
        {
            var count = Count();
            var result = new List<Locator>(count);
            for (int i = 0; i < count; i++)
                result.Add(Nth(i));
            return result;
        }

        // ─── Wait ─────────────────────────────────────────────────────────────────

        /// <summary>Waits until the locator reaches the desired <paramref name="state"/>.</summary>
        public Locator WaitFor(LocatorState state = LocatorState.Visible, int timeoutSeconds = -1)
        {
            if (timeoutSeconds <= 0)
                timeoutSeconds = SimpleSeleniumSupportDefaults.LocatorTimeoutSeconds;

            try
            {
                var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
                while (DateTime.UtcNow < deadline)
                {
                    var elements = ResolveAll();
                    bool satisfied = state switch
                    {
                        LocatorState.Attached => elements.Count > 0,
                        LocatorState.Detached => elements.Count == 0,
                        LocatorState.Visible => elements.Count > 0 && LocatorWaitHelper.SafeDisplayed(elements[0]),
                        LocatorState.Hidden => elements.Count == 0 || !LocatorWaitHelper.SafeDisplayed(elements[0]),
                        _ => false
                    };
                    if (satisfied) return this;
                    _frameRestoreAction?.Invoke(); // restore between polls
                    Thread.Sleep(200);
                }
                throw new WebDriverTimeoutException(
                    $"Timed out after {timeoutSeconds}s waiting for '{_description}' to be {state}.");
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        // ─── State queries (non-throwing) ─────────────────────────────────────────

        /// <summary>Returns <c>true</c> if the element exists and is visible.</summary>
        public bool IsVisible()
        {
            try
            {
                var elements = ResolveAll();
                return elements.Count > 0 && LocatorWaitHelper.SafeDisplayed(elements[0]);
            }
            catch { return false; }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Returns <c>true</c> if the element does not exist or is not visible.</summary>
        public bool IsHidden()
        {
            try
            {
                var elements = ResolveAll();
                return elements.Count == 0 || !LocatorWaitHelper.SafeDisplayed(elements[0]);
            }
            catch { return true; }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Returns <c>true</c> if the element exists and is enabled.</summary>
        public bool IsEnabled()
        {
            try
            {
                var elements = ResolveAll();
                return elements.Count > 0 && LocatorWaitHelper.SafeEnabled(elements[0]);
            }
            catch { return false; }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Returns <c>true</c> if the element is disabled (or not found).</summary>
        public bool IsDisabled() => !IsEnabled();

        /// <summary>Returns <c>true</c> if the checkbox/radio is selected.</summary>
        public bool IsChecked()
        {
            try
            {
                var elements = ResolveAll();
                if (elements.Count == 0) return false;
                var element = elements[0];
                return element.Selected || string.Equals(element.GetAttribute("checked"), "true", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Returns <c>true</c> if the element is visible, enabled, and not readonly.</summary>
        public bool IsEditable()
        {
            try
            {
                var elements = ResolveAll();
                if (elements.Count == 0) return false;
                var element = elements[0];
                return LocatorWaitHelper.SafeDisplayed(element)
                    && LocatorWaitHelper.SafeEnabled(element)
                    && element.GetAttribute("readonly") == null;
            }
            catch { return false; }
            finally { _frameRestoreAction?.Invoke(); }
        }

        // ─── Content getters ──────────────────────────────────────────────────────

        /// <summary>Returns the element's <c>innerText</c> or <c>null</c> if not found.</summary>
        public string? TextContent()
        {
            try { return ResolveOne(new ActionOptions { Force = true }, WaitUntil.Exists).Text; }
            catch { return null; }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Returns the JS <c>innerText</c> property.</summary>
        public string? InnerText()
        {
            try
            {
                var element = ResolveOne(new ActionOptions { Force = true }, WaitUntil.Exists);
                return Js().ExecuteScript("return arguments[0].innerText;", element) as string;
            }
            catch { return null; }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Returns the JS <c>innerHTML</c> property.</summary>
        public string? InnerHTML()
        {
            try
            {
                var element = ResolveOne(new ActionOptions { Force = true }, WaitUntil.Exists);
                return Js().ExecuteScript("return arguments[0].innerHTML;", element) as string;
            }
            catch { return null; }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Returns the <c>value</c> attribute of a form field.</summary>
        public string? InputValue()
        {
            try { return ResolveOne(new ActionOptions { Force = true }, WaitUntil.Exists).GetAttribute("value"); }
            catch { return null; }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Returns the named attribute value, or <c>null</c> if not found.</summary>
        public string? GetAttribute(string name)
        {
            try { return ResolveOne(new ActionOptions { Force = true }, WaitUntil.Exists).GetAttribute(name); }
            catch { return null; }
            finally { _frameRestoreAction?.Invoke(); }
        }

        // ─── Actions ──────────────────────────────────────────────────────────────

        /// <summary>Clicks the element. Waits for clickability first (unless <c>Force=true</c>).</summary>
        public void Click(ActionOptions? options = null)
        {
            try
            {
                options ??= new ActionOptions();
                var element = ResolveOne(options, WaitUntil.Clickable);
                if (options.ScrollIntoView) TryScrollIntoView(element);
                element.Click();
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Double-clicks the element.</summary>
        public void DblClick(ActionOptions? options = null)
        {
            try
            {
                options ??= new ActionOptions();
                var element = ResolveOne(options, WaitUntil.Clickable);
                if (options.ScrollIntoView) TryScrollIntoView(element);
                new Actions(_driver).DoubleClick(element).Perform();
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Right-clicks (context-menu click) the element.</summary>
        public void RightClick(ActionOptions? options = null)
        {
            try
            {
                options ??= new ActionOptions();
                var element = ResolveOne(options, WaitUntil.Clickable);
                if (options.ScrollIntoView) TryScrollIntoView(element);
                new Actions(_driver).ContextClick(element).Perform();
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Clears the field then types <paramref name="value"/>. Best for inputs.</summary>
        public void Fill(string value, ActionOptions? options = null)
        {
            try
            {
                var element = ResolveOne(options, WaitUntil.Enabled);
                element.Clear();
                element.SendKeys(value);
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Types <paramref name="text"/> one character at a time (fires key events per char).</summary>
        public void Type(string text, ActionOptions? options = null)
        {
            try
            {
                var element = ResolveOne(options, WaitUntil.Enabled);
                foreach (char character in text)
                    element.SendKeys(character.ToString());
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Types <paramref name="text"/> with an optional <paramref name="delayMs"/> between characters.</summary>
        public void PressSequentially(string text, int delayMs = 50, ActionOptions? options = null)
        {
            try
            {
                var element = ResolveOne(options, WaitUntil.Enabled);
                foreach (char character in text)
                {
                    element.SendKeys(character.ToString());
                    if (delayMs > 0) Thread.Sleep(delayMs);
                }
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Clears the field value.</summary>
        public void Clear(ActionOptions? options = null)
        {
            try
            {
                var element = ResolveOne(options, WaitUntil.Enabled);
                element.Clear();
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>
        /// Presses a key or key combination. Supports Playwright key names:
        /// <c>Enter</c>, <c>Tab</c>, <c>Escape</c>, <c>ArrowUp</c>, <c>Control+A</c>, etc.
        /// </summary>
        public void Press(string key, ActionOptions? options = null)
        {
            try
            {
                var element = ResolveOne(options, WaitUntil.Visible);
                element.SendKeys(MapKey(key));
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Checks a checkbox or radio button (clicks only if not already checked).</summary>
        public void Check(ActionOptions? options = null)
        {
            try
            {
                var element = ResolveOne(options, WaitUntil.Clickable);
                if (!element.Selected) element.Click();
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Unchecks a checkbox (clicks only if currently checked).</summary>
        public void Uncheck(ActionOptions? options = null)
        {
            try
            {
                var element = ResolveOne(options, WaitUntil.Clickable);
                if (element.Selected) element.Click();
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Selects an option in a &lt;select&gt; by its <c>value</c> attribute.</summary>
        public void SelectOption(string value, ActionOptions? options = null)
        {
            try
            {
                var element = ResolveOne(options, WaitUntil.Enabled);
                new SelectElement(element).SelectByValue(value);
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Selects an option in a &lt;select&gt; by its zero-based index.</summary>
        public void SelectOption(int index, ActionOptions? options = null)
        {
            try
            {
                var element = ResolveOne(options, WaitUntil.Enabled);
                new SelectElement(element).SelectByIndex(index);
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Selects an option in a &lt;select&gt; by its visible text.</summary>
        public void SelectOptionByText(string text, ActionOptions? options = null)
        {
            try
            {
                var element = ResolveOne(options, WaitUntil.Enabled);
                new SelectElement(element).SelectByText(text);
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Moves the mouse to hover over the element.</summary>
        public void Hover(ActionOptions? options = null)
        {
            try
            {
                var element = ResolveOne(options, WaitUntil.Visible);
                new Actions(_driver).MoveToElement(element).Perform();
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Gives keyboard focus to the element via JS.</summary>
        public void Focus(ActionOptions? options = null)
        {
            try
            {
                var element = ResolveOne(options, WaitUntil.Visible);
                Js().ExecuteScript("arguments[0].focus();", element);
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Removes keyboard focus from the element via JS.</summary>
        public void Blur(ActionOptions? options = null)
        {
            try
            {
                var element = ResolveOne(options, WaitUntil.Visible);
                Js().ExecuteScript("arguments[0].blur();", element);
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Drags this element and drops it onto <paramref name="target"/>.</summary>
        public void DragTo(Locator target, ActionOptions? options = null)
        {
            try
            {
                var sourceElement = ResolveOne(options, WaitUntil.Visible);
                var targetElement = target.ResolveOne(options, WaitUntil.Visible);
                new Actions(_driver).DragAndDrop(sourceElement, targetElement).Perform();
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Dispatches a DOM event on the element.</summary>
        public void DispatchEvent(string eventName, ActionOptions? options = null)
        {
            try
            {
                var element = ResolveOne(options, WaitUntil.Exists);
                Js().ExecuteScript(
                    $"arguments[0].dispatchEvent(new Event('{eventName}', {{bubbles:true,cancelable:true}}));", element);
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>
        /// Executes a JavaScript snippet with the element passed as <c>arguments[0]</c>.
        /// </summary>
        public T? Evaluate<T>(string script, ActionOptions? options = null)
        {
            try
            {
                var element = ResolveOne(options, WaitUntil.Exists);
                var raw = Js().ExecuteScript(script, element);
                if (raw is T t) return t;
                try { return (T)Convert.ChangeType(raw, typeof(T))!; }
                catch { return default; }
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Scrolls the element into the visible viewport.</summary>
        public void ScrollIntoView(ActionOptions? options = null)
        {
            try
            {
                var element = ResolveOne(options, WaitUntil.Exists);
                Js().ExecuteScript("arguments[0].scrollIntoView({block:'center',inline:'center'});", element);
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>
        /// Takes a screenshot of the element's bounding box. Optionally saves to <paramref name="path"/>.
        /// </summary>
        public byte[] Screenshot(string? path = null)
        {
            try
            {
                var element = ResolveOne(new ActionOptions { Force = true }, WaitUntil.Exists);
                if (element is ITakesScreenshot screenshotCapable)
                {
                    var screenshotData = screenshotCapable.GetScreenshot();
                    if (path != null)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
                        screenshotData.SaveAsFile(path);
                    }
                    return screenshotData.AsByteArray;
                }
                throw new InvalidOperationException("The element driver does not support screenshots.");
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>
        /// Sets files on an <c>&lt;input type="file"&gt;</c> by sending the absolute path via SendKeys.
        /// </summary>
        public void SetInputFiles(string filePath, ActionOptions? options = null)
        {
            try
            {
                var element = ResolveOne(options, WaitUntil.Exists);
                element.SendKeys(Path.GetFullPath(filePath));
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        /// <summary>Sets multiple files on a multi-file input.</summary>
        public void SetInputFiles(IEnumerable<string> filePaths, ActionOptions? options = null)
        {
            try
            {
                var element = ResolveOne(options, WaitUntil.Exists);
                // Remove restrictions so multi-file paths can be fed in
                try { Js().ExecuteScript("arguments[0].removeAttribute('accept');", element); } catch { /* ignore */ }
                foreach (var path in filePaths)
                    element.SendKeys(Path.GetFullPath(path));
            }
            finally { _frameRestoreAction?.Invoke(); }
        }

        // ─── Convenience / element access ─────────────────────────────────────────

        /// <summary>
        /// Resolves and returns the underlying <see cref="IWebElement"/>.
        /// Waits up to 1 second; does not check actionability.
        /// </summary>
        public IWebElement Element =>
            ResolveOne(new ActionOptions { TimeoutSeconds = 1, Force = true }, WaitUntil.Exists);

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private IJavaScriptExecutor Js() => (IJavaScriptExecutor)_driver;

        private void TryScrollIntoView(IWebElement el)
        {
            try { Js().ExecuteScript("arguments[0].scrollIntoView({block:'center',inline:'center'});", el); }
            catch { /* non-fatal */ }
        }

        private static string? SafeText(IWebElement el)
        {
            try { return el.Text; }
            catch { return null; }
        }

        private static bool AreSameElement(IWebElement firstElement, IWebElement secondElement, IWebDriver driver)
        {
            try
            {
                var jsExecutor = (IJavaScriptExecutor)driver;
                var result = jsExecutor.ExecuteScript("return arguments[0] === arguments[1];", firstElement, secondElement);
                return result is bool boolValue && boolValue;
            }
            catch
            {
                try { return firstElement.Equals(secondElement); }
                catch { return false; }
            }
        }

        private static string MapKey(string key) => key switch
        {
            "Enter" => Keys.Enter,
            "Tab" => Keys.Tab,
            "Escape" or "Esc" => Keys.Escape,
            "Backspace" => Keys.Backspace,
            "Delete" => Keys.Delete,
            "Space" or " " => Keys.Space,
            "ArrowUp" or "Up" => Keys.ArrowUp,
            "ArrowDown" or "Down" => Keys.ArrowDown,
            "ArrowLeft" or "Left" => Keys.ArrowLeft,
            "ArrowRight" or "Right" => Keys.ArrowRight,
            "Home" => Keys.Home,
            "End" => Keys.End,
            "PageUp" => Keys.PageUp,
            "PageDown" => Keys.PageDown,
            "F1" => Keys.F1,
            "F2" => Keys.F2,
            "F3" => Keys.F3,
            "F4" => Keys.F4,
            "F5" => Keys.F5,
            "F6" => Keys.F6,
            "F10" => Keys.F10,
            "F11" => Keys.F11,
            "F12" => Keys.F12,
            _ when key.StartsWith("Control+", StringComparison.OrdinalIgnoreCase)
                => Keys.Control + key[8..] + Keys.Control,
            _ when key.StartsWith("Shift+", StringComparison.OrdinalIgnoreCase)
                => Keys.Shift + key[6..] + Keys.Shift,
            _ when key.StartsWith("Alt+", StringComparison.OrdinalIgnoreCase)
                => Keys.Alt + key[4..] + Keys.Alt,
            _ => key
        };

        /// <summary>Returns the human-readable description of this locator.</summary>
        public override string ToString() => _description;
    }
}
