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
    }
}
