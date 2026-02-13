using OpenQA.Selenium;
using System.Collections.ObjectModel;
using System.Linq;

namespace SimpleSeleniumSupport.Selectors
{
    /// <summary>
    /// Playwright-style role locator implemented as Selenium By
    /// </summary>
    public sealed class ByRole : OpenQA.Selenium.By
    {
        private readonly string _role;
        private readonly string _name;
        private readonly LocatorOptions _options;

        public ByRole(string role, string name, LocatorOptions options)
        {
            _role = role;
            _name = name;
            _options = options ?? new LocatorOptions();
        }

        public override IWebElement FindElement(ISearchContext context)
        {
            var elements = FindElements(context);
            if (elements.Count == 0)
                throw new NoSuchElementException(
                    $"No element found with role='{_role}' name='{_name}'");

            return elements[0];
        }

        public override ReadOnlyCollection<IWebElement> FindElements(ISearchContext context)
        {
            if (context is not IWebDriver driver)
                return new ReadOnlyCollection<IWebElement>(new IWebElement[0]);

            // Simple, safe heuristic (baseline)
            var xpath = BuildRoleXPath();
            var found = driver.FindElements(
                OpenQA.Selenium.By.XPath(xpath));

            return new ReadOnlyCollection<IWebElement>(
                found.ToList());
        }

        private string BuildRoleXPath()
        {
            if (string.IsNullOrEmpty(_name))
                return $"//*[@role='{_role}' or self::{_role}]";

            return
                $"//*[(@role='{_role}' or self::{_role}) " +
                $"and contains(normalize-space(.), '{_name}')]";
        }

        public override string ToString()
            => $"ByRole(role='{_role}', name='{_name}')";
    }
}
