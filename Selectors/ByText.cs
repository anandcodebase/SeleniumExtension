using OpenQA.Selenium;
using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace SimpleSeleniumSupport.Selectors
{
    internal sealed class ByText : OpenQA.Selenium.By
    {
        private readonly string _text;
        private readonly LocatorOptions _options;

        public ByText(string text, LocatorOptions options)
        {
            _text = text;
            _options = options;
        }

        public override IWebElement FindElement(ISearchContext context)
        {
            var list = FindElements(context);
            if (list.Count == 0)
                throw new NoSuchElementException($"No element found with text='{_text}'");
            return list[0];
        }

        public override ReadOnlyCollection<IWebElement> FindElements(ISearchContext context)
        {
            var prefix = context is IWebDriver ? "//" : ".//";

            var xpath = _options.ExactMatch
                ? $"{prefix}*[normalize-space(.)={XPathHelper.Quote(_text)}]"
                : $"{prefix}*[contains(translate(normalize-space(.), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), {XPathHelper.QuotePartial(_text)})]";

            return new ReadOnlyCollection<IWebElement>(
                context.FindElements(OpenQA.Selenium.By.XPath(xpath)).ToList());
        }

        public override string ToString() => $"By.Text('{_text}')";
    }
}
