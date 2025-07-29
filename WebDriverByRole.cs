using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using SeleniumExtras.WaitHelpers;
using System;
using System.Collections.Generic;

namespace SeleniumExtention
{
    public static class WebDriverByRole
    {
        /// <summary>
        /// Finds an element by its ARIA role, waiting for it to be clickable.
        /// This method is useful when you only need to specify the role and not a specific accessible name.
        /// </summary>
        /// <param name="driver">The IWebDriver instance.</param>
        /// <param name="role">The ARIA role of the element (e.g., 'button', 'link', 'checkbox', 'textbox').</param>
        /// <param name="timeoutInSeconds">The maximum time to wait for the element to be clickable, in seconds. Defaults to 10 seconds.</param>
        /// <returns>The found IWebElement.</returns>
        /// <exception cref="WebDriverTimeoutException">Thrown if the element is not found or not clickable within the specified timeout.</exception>
        /// <example>
        /// <code>
        /// IWebDriver driver = new ChromeDriver();
        /// // ... navigate to page ...
        /// IWebElement statusElement = driver.GetByRole("status");
        /// Console.WriteLine($"Status: {statusElement.Text}");
        /// </code>
        /// </example>
        public static IWebElement GetByRole(this IWebDriver driver, string role, int timeoutInSeconds = 10)
        {
            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutInSeconds));
            string xpath = $"//*[@role='{role}']";
            return wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath(xpath)));
        }

        /// <summary>
        /// Finds an element by its ARIA role and its accessible name, waiting for it to be clickable.
        /// The accessible name is checked against the element's text content, 'aria-label', and 'title' attributes.
        /// This provides a robust way to locate elements semantically.
        /// </summary>
        /// <param name="driver">The IWebDriver instance.</param>
        /// <param name="role">The ARIA role of the element (e.g., 'button', 'link', 'checkbox', 'textbox').</param>
        /// <param name="accessibleName">The expected accessible name (text content, aria-label, or title attribute value).</param>
        /// <param name="timeoutInSeconds">The maximum time to wait for the element to be clickable, in seconds. Defaults to 10 seconds.</param>
        /// <returns>The found IWebElement.</returns>
        /// <exception cref="WebDriverTimeoutException">Thrown if the element is not found or not clickable within the specified timeout.</exception>
        /// <example>
        /// <code>
        /// IWebDriver driver = new ChromeDriver();
        /// // ... navigate to page ...
        /// IWebElement submitButton = driver.GetByRoleAndName("button", "Submit", 15); // Wait up to 15 seconds
        /// submitButton.Click();
        /// </code>
        /// </example>
        public static IWebElement GetByRoleAndName(this IWebDriver driver, string role, string accessibleName, int timeoutInSeconds = 10)
        {
            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutInSeconds));
            string xpath = $"//*[@role='{role}' and (text()='{accessibleName}' or @aria-label='{accessibleName}' or @title='{accessibleName}')]";
            return wait.Until(ExpectedConditions.ElementToBeClickable(By.XPath(xpath)));
        }

        /// <summary>
        /// Finds a list of elements by their ARIA role, waiting for at least one to be present.
        /// This method is useful when you expect multiple elements with the same role.
        /// </summary>
        /// <param name="driver">The IWebDriver instance.</param>
        /// <param name="role">The ARIA role of the elements.</param>
        /// <param name="timeoutInSeconds">The maximum time to wait, in seconds. Defaults to 10 seconds.</param>
        /// <returns>A list of IWebElements matching the role.</returns>
        /// <exception cref="WebDriverTimeoutException">Thrown if no elements are found within the specified timeout.</exception>
        /// <example>
        /// <code>
        /// IWebDriver driver = new ChromeDriver();
        /// // ... navigate to page ...
        /// IReadOnlyCollection&lt;IWebElement&gt; allLinks = driver.GetElementsByRole("link");
        /// foreach (var link in allLinks)
        /// {
        ///     Console.WriteLine(link.Text);
        /// }
        /// </code>
        /// </example>
        public static IReadOnlyCollection<IWebElement> GetElementsByRole(this IWebDriver driver, string role, int timeoutInSeconds = 10)
        {
            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutInSeconds));
            string xpath = $"//*[@role='{role}']";
            return wait.Until(d => d.FindElements(By.XPath(xpath)).Count > 0 ? d.FindElements(By.XPath(xpath)) : null);
        }

        /// <summary>
        /// Finds a list of elements by their ARIA role and accessible name, waiting for at least one to be present.
        /// </summary>
        /// <param name="driver">The IWebDriver instance.</param>
        /// <param name="role">The ARIA role of the elements.</param>
        /// <param name="accessibleName">The expected accessible name.</param>
        /// <param name="timeoutInSeconds">The maximum time to wait, in seconds. Defaults to 10 seconds.</param>
        /// <returns>A list of IWebElements matching the role and name.</returns>
        /// <exception cref="WebDriverTimeoutException">Thrown if no elements are found within the specified timeout.</exception>
        /// <example>
        /// <code>
        /// IWebDriver driver = new ChromeDriver();
        /// // ... navigate to page ...
        /// IReadOnlyCollection&lt;IWebElement&gt; allHelpButtons = driver.GetElementsByRoleAndName("button", "Help");
        /// foreach (var button in allHelpButtons)
        /// {
        ///     Console.WriteLine(button.GetAttribute("id"));
        /// }
        /// </code>
        /// </example>
        public static IReadOnlyCollection<IWebElement> GetElementsByRoleAndName(this IWebDriver driver, string role, string accessibleName, int timeoutInSeconds = 10)
        {
            WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(timeoutInSeconds));
            string xpath = $"//*[@role='{role}' and (text()='{accessibleName}' or @aria-label='{accessibleName}' or @title='{accessibleName}')]";
            return wait.Until(d => d.FindElements(By.XPath(xpath)).Count > 0 ? d.FindElements(By.XPath(xpath)) : null);
        }
    }
}
