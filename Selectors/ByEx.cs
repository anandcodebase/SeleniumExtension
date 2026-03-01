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
            string? accessibleName = null,
            LocatorOptions? options = null)
        {
            options ??= new LocatorOptions();
            return new ByRole(role, accessibleName, options);
        }

        public static OpenQA.Selenium.By Role(
            string role,
            string? accessibleName = null,
            bool exactMatch = false,
            bool caseSensitive = false,
            int timeoutSeconds = 10,
            int pollingMs = 200,
            WaitUntil wait = WaitUntil.Visible)
        {
            return new ByRole(role, accessibleName, exactMatch, caseSensitive, timeoutSeconds, pollingMs, wait);
        }

        public static OpenQA.Selenium.By Text(
            string text,
            LocatorOptions? options = null)
        {
            options ??= new LocatorOptions();
            return new ByText(text, options);
        }

        public static OpenQA.Selenium.By Text(
            string text,
            bool exactMatch = false,
            bool caseSensitive = false,
            int timeoutSeconds = 10,
            int pollingMs = 200,
            WaitUntil wait = WaitUntil.Visible)
        {
            return new ByText(text, exactMatch, caseSensitive, timeoutSeconds, pollingMs, wait);
        }

        public static OpenQA.Selenium.By TestId(string testId, LocatorOptions? options = null)
        {
            options ??= new LocatorOptions();
            return new ByTestId(testId, options);
        }

        public static OpenQA.Selenium.By TestId(
            string testId,
            int timeoutSeconds = 10,
            int pollingMs = 200,
            WaitUntil wait = WaitUntil.Visible)
        {
            return new ByTestId(testId, timeoutSeconds, pollingMs, wait);
        }
    }
}
