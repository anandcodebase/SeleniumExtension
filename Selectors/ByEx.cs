using OpenQA.Selenium;

namespace SimpleSeleniumSupport.Selectors
{
    /// <summary>
    /// Selenium-compatible locator factory extensions.
    /// Never shadows OpenQA.Selenium.By.
    /// </summary>
    public static class ByEx
    {
        public static OpenQA.Selenium.By Role(
            string role,
            string accessibleName = null,
            LocatorOptions options = null)
        {
            options ??= new LocatorOptions();
            return new ByRole(role, accessibleName, options);
        }

        public static OpenQA.Selenium.By Text(
            string text,
            LocatorOptions options = null)
        {
            options ??= new LocatorOptions();
            return new ByText(text, options);
        }

        public static OpenQA.Selenium.By TestId(string testId)
        {
            var css =
                $"[data-testid='{EscapeCss(testId)}']," +
                $"[data-test-id='{EscapeCss(testId)}']," +
                $"[data-test='{EscapeCss(testId)}']";

            return OpenQA.Selenium.By.CssSelector(css);
        }

        private static string EscapeCss(string value)
            => value?.Replace("'", "\\'") ?? string.Empty;
    }
}
