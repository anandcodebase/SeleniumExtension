using OpenQA.Selenium;
using System;

namespace SimpleSeleniumSupport.Input
{
    /// <summary>
    /// Extension methods that expose the Playwright-style <see cref="SeleniumMouse"/> and
    /// <see cref="SeleniumKeyboard"/> APIs directly from <see cref="IWebDriver"/>.
    /// </summary>
    /// <example>
    /// <code>
    /// driver.Mouse().Click(100, 200);
    /// driver.Mouse().Wheel(0, 300);
    /// driver.Keyboard().Type("Hello World");
    /// driver.Keyboard().Press("Control+A");
    /// </code>
    /// </example>
    public static class InputExtensions
    {
        /// <summary>
        /// Returns a <see cref="SeleniumMouse"/> bound to <paramref name="driver"/> for
        /// absolute-coordinate click, hover, drag, and scroll operations.
        /// </summary>
        public static SeleniumMouse Mouse(this IWebDriver driver)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            return new SeleniumMouse(driver);
        }

        /// <summary>
        /// Returns a <see cref="SeleniumKeyboard"/> bound to <paramref name="driver"/> for
        /// type, press, and key-hold operations.
        /// </summary>
        public static SeleniumKeyboard Keyboard(this IWebDriver driver)
        {
            if (driver == null) throw new ArgumentNullException(nameof(driver));
            return new SeleniumKeyboard(driver);
        }
    }
}
