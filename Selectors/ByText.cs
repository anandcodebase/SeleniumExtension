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
            if (context is not IWebDriver driver)
                return new ReadOnlyCollection<IWebElement>(Array.Empty<IWebElement>());

            var lowered = _text.ToLowerInvariant();

            var xpath = _options.ExactMatch
                ? $"//*[normalize-space(.)='{_text}']"
                : $"//*[contains(translate(normalize-space(.), 'ABCDEFGHIJKLMNOPQRSTUVWXYZ', 'abcdefghijklmnopqrstuvwxyz'), '{lowered}')]";

            return new ReadOnlyCollection<IWebElement>(driver.FindElements(OpenQA.Selenium.By.XPath(xpath)).ToList());
        }

        public override string ToString() => $"By.Text('{_text}')";
    }
}
