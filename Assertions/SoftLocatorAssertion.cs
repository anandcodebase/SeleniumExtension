using SimpleSeleniumSupport.Selectors;
using System;
using System.Collections.Generic;

namespace SimpleSeleniumSupport.Assertions
{
    /// <summary>
    /// Non-throwing version of <see cref="LocatorAssertion"/> used by <see cref="SoftAssertions"/>.
    /// Catches <see cref="WebAssertionException"/> and records it instead of rethrowing.
    /// </summary>
    public sealed class SoftLocatorAssertion
    {
        private readonly Locator _locator;
        private readonly List<WebAssertionException> _failures;
        private readonly bool _negate;

        internal SoftLocatorAssertion(Locator locator, List<WebAssertionException> failures, bool negate = false)
        {
            _locator = locator;
            _failures = failures;
            _negate = negate;
        }

        /// <summary>Inverts this soft assertion.</summary>
        public SoftLocatorAssertion Not => new SoftLocatorAssertion(_locator, _failures, !_negate);

        // ─── Forwarded assertions ──────────────────────────────────────────────────

        /// <summary>Soft assertion: element is visible.</summary>
        public void IsVisible()       => Run(() => Inner.IsVisible());
        /// <summary>Soft assertion: element is hidden.</summary>
        public void IsHidden()        => Run(() => Inner.IsHidden());
        /// <summary>Soft assertion: element is enabled.</summary>
        public void IsEnabled()       => Run(() => Inner.IsEnabled());
        /// <summary>Soft assertion: element is disabled.</summary>
        public void IsDisabled()      => Run(() => Inner.IsDisabled());
        /// <summary>Soft assertion: checkbox/radio is checked.</summary>
        public void IsChecked()       => Run(() => Inner.IsChecked());
        /// <summary>Soft assertion: element is editable (visible, enabled, not readonly).</summary>
        public void IsEditable()      => Run(() => Inner.IsEditable());
        /// <summary>Soft assertion: element has no text content and no value.</summary>
        public void IsEmpty()         => Run(() => Inner.IsEmpty());

        /// <summary>Soft assertion: element text equals <paramref name="expected"/>.</summary>
        public void HasText(string expected)              => Run(() => Inner.HasText(expected));
        /// <summary>Soft assertion: element text contains <paramref name="substring"/>.</summary>
        public void ContainsText(string substring)        => Run(() => Inner.ContainsText(substring));
        /// <summary>Soft assertion: input value equals <paramref name="expected"/>.</summary>
        public void HasValue(string expected)             => Run(() => Inner.HasValue(expected));
        /// <summary>Soft assertion: named attribute equals <paramref name="value"/>.</summary>
        public void HasAttribute(string name, string value) => Run(() => Inner.HasAttribute(name, value));
        /// <summary>Soft assertion: element has CSS class <paramref name="className"/>.</summary>
        public void HasClass(string className)            => Run(() => Inner.HasClass(className));
        /// <summary>Soft assertion: exactly <paramref name="count"/> elements match.</summary>
        public void HasCount(int count)                   => Run(() => Inner.HasCount(count));
        /// <summary>Soft assertion: element innerHTML contains <paramref name="html"/>.</summary>
        public void ContainsHTML(string html)             => Run(() => Inner.ContainsHTML(html));

        private LocatorAssertion Inner => new LocatorAssertion(_locator, _negate);

        private void Run(Action assertion)
        {
            try { assertion(); }
            catch (WebAssertionException ex) { _failures.Add(ex); }
        }
    }
}
