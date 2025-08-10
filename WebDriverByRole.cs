using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System;
using System.Collections.Generic;

namespace SimpleSeleniumSupport
{
    public static class WebDriverByRole
    {

        public static IWebElement GetByRole(this IWebDriver driver, string role, int timeoutInSeconds = 10)
        {
            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutInSeconds));
            string xpath = $"//*[@role='{role}']";
            return wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath(xpath)));
        }


        public static IWebElement GetByRoleAndName(this IWebDriver driver, string role, string accessibleName, int timeoutInSeconds = 10)
        {
            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutInSeconds));
            string xpath = $"//*[@role='{role}' and (text()='{accessibleName}' or @aria-label='{accessibleName}' or @title='{accessibleName}')]";
            return wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath(xpath)));
        }


        public static IReadOnlyCollection<IWebElement> GetElementsByRole(this IWebDriver driver, string role, int timeoutInSeconds = 10)
        {
            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutInSeconds));
            string xpath = $"//*[@role='{role}']";
            return wait.Until(d => d.FindElements(By.XPath(xpath)).Count > 0 ? d.FindElements(By.XPath(xpath)) : null);
        }


        public static IReadOnlyCollection<IWebElement> GetElementsByRoleAndName(this IWebDriver driver, string role, string accessibleName, int timeoutInSeconds = 10)
        {
            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutInSeconds));
            string xpath = $"//*[@role='{role}' and (text()='{accessibleName}' or @aria-label='{accessibleName}' or @title='{accessibleName}')]";
            return wait.Until(d => d.FindElements(By.XPath(xpath)).Count > 0 ? d.FindElements(By.XPath(xpath)) : null);
        }

        private static IWebElement FindElement(IWebDriver driver, By by, int timeoutInSeconds)
        {
            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutInSeconds));
            return wait.Until(ExpectedConditions.ElementIsVisible(by));
        }

        private static IReadOnlyCollection<IWebElement> FindElements(IWebDriver driver, By by, int timeoutInSeconds)
        {
            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutInSeconds));
            return wait.Until(d => d.FindElements(by).Any() ? d.FindElements(by) : null);
        }

        public static IWebElement GetByText(this IWebDriver driver, string text, int timeoutInSeconds = 10)
        {
            return FindElement(driver, By.XPath($"//*[text()='{text}']"), timeoutInSeconds);
        }

        public static IReadOnlyCollection<IWebElement> GetElementsByText(this IWebDriver driver, string text, int timeoutInSeconds = 10)
        {
            return FindElements(driver, By.XPath($"//*[text()='{text}']"), timeoutInSeconds);
        }

        public static IWebElement GetByLabel(this IWebDriver driver, string label, int timeoutInSeconds = 10)
        {
            return FindElement(driver, By.XPath($"//*[@aria-label='{label}']"), timeoutInSeconds);
        }

        public static IWebElement GetByPlaceholder(this IWebDriver driver, string placeholder, int timeoutInSeconds = 10)
        {
            return FindElement(driver, By.XPath($"//*[@placeholder='{placeholder}']"), timeoutInSeconds);
        }

        public static IWebElement GetByAltText(this IWebDriver driver, string altText, int timeoutInSeconds = 10)
        {
            return FindElement(driver, By.XPath($"//*[@alt='{altText}']"), timeoutInSeconds);
        }

        public static IWebElement GetByTitle(this IWebDriver driver, string title, int timeoutInSeconds = 10)
        {
            return FindElement(driver, By.XPath($"//*[@title='{title}']"), timeoutInSeconds);
        }

        public static IWebElement GetByTestId(this IWebDriver driver, string testId, int timeoutInSeconds = 10)
        {
            return FindElement(driver, By.XPath($"//*[@data-testid='{testId}']"), timeoutInSeconds);
        }

    }
}
