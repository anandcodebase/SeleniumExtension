using OpenQA.Selenium;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;

namespace SimpleSeleniumSupport.Selectors
{
    /// <summary>
    /// Playwright-style role locator implemented as Selenium By.
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

        public ByRole(string role, string name = null,
            bool exactMatch = false,
            bool caseSensitive = false,
            int timeoutSeconds = 10,
            int pollingMs = 200,
            WaitUntil wait = WaitUntil.Visible)
            : this(role, name, new LocatorOptions
            {
                ExactMatch = exactMatch,
                CaseSensitive = caseSensitive,
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
                    $"No element found with role='{_role}' name='{_name}'");

            return elements[0];
        }

        public override ReadOnlyCollection<IWebElement> FindElements(ISearchContext context)
        {
            var prefix = context is IWebDriver ? "//" : ".//";
            var xpath = BuildRoleXPath(prefix);
            var found = context.FindElements(
                OpenQA.Selenium.By.XPath(xpath));

            return new ReadOnlyCollection<IWebElement>(
                found.ToList());
        }

        private string BuildRoleXPath(string prefix)
        {
            var roleQuoted = XPathHelper.Quote(_role);
            var selfClause = IsValidElementName(_role) ? $" or self::{_role}" : "";

            if (string.IsNullOrEmpty(_name))
                return $"{prefix}*[@role={roleQuoted}{selfClause}]";

            var nameQuoted = XPathHelper.Quote(_name);
            return $"{prefix}*[(@role={roleQuoted}{selfClause}) and contains(normalize-space(.), {nameQuoted})]";
        }

        private static bool IsValidElementName(string name)
            => !string.IsNullOrEmpty(name) && Regex.IsMatch(name, @"^[a-zA-Z][a-zA-Z0-9]*$");

        public override string ToString()
            => $"ByRole(role='{_role}', name='{_name}')";
    }
}
