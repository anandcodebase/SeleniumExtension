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

        public static OpenQA.Selenium.By TestId(string testId, LocatorOptions options = null)
        {
            options ??= new LocatorOptions();
            return new ByTestId(testId, options);
        }
    }
}
