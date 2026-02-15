using OpenQA.Selenium;
using System.Collections.ObjectModel;
using System.Linq;

namespace SimpleSeleniumSupport.Selectors
{
    /// <summary>
    /// Playwright-style test ID locator implemented as Selenium By.
    /// Searches data-testid, data-test-id, and data-test attributes.
    /// </summary>
    public sealed class ByTestId : OpenQA.Selenium.By
    {
        private readonly string _testId;
        private readonly LocatorOptions _options;

        public ByTestId(string testId, LocatorOptions options)
        {
            _testId = testId;
            _options = options ?? new LocatorOptions();
        }

        public ByTestId(string testId,
            int timeoutSeconds = 10,
            int pollingMs = 200,
            WaitUntil wait = WaitUntil.Visible)
            : this(testId, new LocatorOptions
            {
                TimeoutSeconds = timeoutSeconds,
                PollingMs = pollingMs,
                Wait = wait
            })
        { }

        public override IWebElement FindElement(ISearchContext context)
        {
            var elements = FindElements(context);
            if (elements.Count == 0)
                throw new NoSuchElementException(
                    $"No element found with testId='{_testId}'");
            return elements[0];
        }

        public override ReadOnlyCollection<IWebElement> FindElements(ISearchContext context)
        {
            var escaped = CssEscaper.EscapeAttributeValue(_testId);
            var css = $"[data-testid='{escaped}'],[data-test-id='{escaped}'],[data-test='{escaped}']";
            var found = context.FindElements(OpenQA.Selenium.By.CssSelector(css));
            return new ReadOnlyCollection<IWebElement>(found.ToList());
        }

        public override string ToString()
            => $"ByTestId('{_testId}')";
    }
}
