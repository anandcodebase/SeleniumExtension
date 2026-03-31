using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;
using System.Threading;

namespace SimpleSeleniumSupport.Input
{
    /// <summary>
    /// Playwright-style keyboard API for typing text, pressing keys, and holding modifiers.
    /// Obtain via <c>driver.Keyboard()</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// driver.Keyboard().Type("Hello World");
    /// driver.Keyboard().Press("Enter");
    /// driver.Keyboard().Press("Control+A");
    /// driver.Keyboard().Down("Shift").Type("hello").Up("Shift");
    /// driver.Keyboard().InsertText("fast paste — no key events");
    /// driver.Keyboard().PressSequentially("abc", delayMs: 100);
    /// </code>
    /// </example>
    public sealed class SeleniumKeyboard
    {
        private readonly IWebDriver _driver;

        internal SeleniumKeyboard(IWebDriver driver) => _driver = driver;

        // ─── Typing ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Fires key-down / key-press / key-up events for each character in <paramref name="text"/>.
        /// Sends to the currently focused element.
        /// </summary>
        public SeleniumKeyboard Type(string text)
        {
            new Actions(_driver).SendKeys(text).Perform();
            return this;
        }

        /// <summary>
        /// Injects <paramref name="text"/> directly into the focused element via JS
        /// without firing individual key events — equivalent to a fast paste.
        /// </summary>
        public SeleniumKeyboard InsertText(string text)
        {
            ((IJavaScriptExecutor)_driver)
                .ExecuteScript(
                    "var el = document.activeElement; if(el) el.value += arguments[0];",
                    text);
            return this;
        }

        /// <summary>Types each character with an optional <paramref name="delayMs"/> between keystrokes.</summary>
        public SeleniumKeyboard PressSequentially(string text, int delayMs = 50)
        {
            foreach (var character in text)
            {
                new Actions(_driver).SendKeys(character.ToString()).Perform();
                if (delayMs > 0) Thread.Sleep(delayMs);
            }
            return this;
        }

        // ─── Key press / hold ─────────────────────────────────────────────────────

        /// <summary>
        /// Presses and releases a key or compound shortcut (<c>Enter</c>, <c>Control+A</c>, etc.).
        /// </summary>
        public SeleniumKeyboard Press(string key)
        {
            new Actions(_driver).SendKeys(KeyMapper.Map(key)).Perform();
            return this;
        }

        /// <summary>
        /// Holds the key (key-down without key-up). Use <see cref="Up"/> to release.
        /// Useful for modifier-key combinations.
        /// </summary>
        public SeleniumKeyboard Down(string key)
        {
            new Actions(_driver).KeyDown(KeyMapper.Map(key)).Perform();
            return this;
        }

        /// <summary>Releases a previously held key (key-up).</summary>
        public SeleniumKeyboard Up(string key)
        {
            new Actions(_driver).KeyUp(KeyMapper.Map(key)).Perform();
            return this;
        }
    }
}
