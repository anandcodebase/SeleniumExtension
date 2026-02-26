using OpenQA.Selenium;
using System;
using System.Collections.ObjectModel;

namespace SimpleSeleniumSupport.Selectors
{
    internal static class RoleXPathBuilder
    {
        internal static string Build(string role, LocatorOptions options)
        {
            var r = role.ToLowerInvariant();
            return r switch
            {
                "button" => "//button|//input[@type='submit']|//input[@type='button']",
                "link" => "//a[@href]",
                "textbox" => "//input[not(@type) or @type='text']|//textarea",
                "checkbox" => "//input[@type='checkbox']",
                "radio" => "//input[@type='radio']",
                "img" => "//img",
                "heading" => "//h1|//h2|//h3|//h4|//h5|//h6",
                _ => $"//*[@role={XPathHelper.Quote(role)}]"
            };
        }

        internal static ReadOnlyCollection<IWebElement> Empty()
            => new(Array.Empty<IWebElement>());
    }
}
