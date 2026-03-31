using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;

namespace SimpleSeleniumSupport.Input
{
    /// <summary>
    /// Playwright-style mouse API for absolute-coordinate clicks, drags, and scrolls.
    /// Obtain via <c>driver.Mouse()</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// driver.Mouse().Click(100, 200);
    /// driver.Mouse().DblClick(300, 400);
    /// driver.Mouse().Hover(150, 250);
    /// driver.Mouse().Down().Move(50, 0).Up().Perform();   // manual drag
    /// driver.Mouse().Wheel(0, 300);                        // scroll down 300px
    /// </code>
    /// </example>
    public sealed class SeleniumMouse
    {
        private readonly IWebDriver _driver;

        internal SeleniumMouse(IWebDriver driver) => _driver = driver;

        // ─── Click variants ────────────────────────────────────────────────────────

        /// <summary>Left-clicks at absolute page coordinates (<paramref name="x"/>, <paramref name="y"/>).</summary>
        public void Click(int x, int y)
        {
            MoveToAbsolute(x, y);
            new Actions(_driver).Click().Perform();
        }

        /// <summary>Double-clicks at (<paramref name="x"/>, <paramref name="y"/>).</summary>
        public void DblClick(int x, int y)
        {
            MoveToAbsolute(x, y);
            new Actions(_driver).DoubleClick().Perform();
        }

        /// <summary>Right-clicks (context menu) at (<paramref name="x"/>, <paramref name="y"/>).</summary>
        public void RightClick(int x, int y)
        {
            MoveToAbsolute(x, y);
            new Actions(_driver).ContextClick().Perform();
        }

        /// <summary>Moves the cursor to (<paramref name="x"/>, <paramref name="y"/>) without clicking.</summary>
        public void Hover(int x, int y) => MoveToAbsolute(x, y);

        // ─── Element-based variants ────────────────────────────────────────────────

        /// <summary>Left-clicks the centre of <paramref name="element"/>.</summary>
        public void Click(IWebElement element) =>
            new Actions(_driver).MoveToElement(element).Click().Perform();

        /// <summary>Double-clicks the centre of <paramref name="element"/>.</summary>
        public void DblClick(IWebElement element) =>
            new Actions(_driver).MoveToElement(element).DoubleClick().Perform();

        /// <summary>Hovers over <paramref name="element"/>.</summary>
        public void Hover(IWebElement element) =>
            new Actions(_driver).MoveToElement(element).Perform();

        // ─── Drag ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Begins a drag at the current cursor position.
        /// Returns a <see cref="MouseChain"/> to move and release.
        /// </summary>
        public MouseChain Down()
        {
            var actions = new Actions(_driver);
            actions.ClickAndHold();
            return new MouseChain(_driver, actions);
        }

        /// <summary>Drags from <paramref name="source"/> to <paramref name="target"/>.</summary>
        public void DragTo(IWebElement source, IWebElement target) =>
            new Actions(_driver).DragAndDrop(source, target).Perform();

        // ─── Scroll ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Scrolls the page by (<paramref name="deltaX"/>, <paramref name="deltaY"/>) pixels
        /// using <c>window.scrollBy</c> via JavaScript.
        /// </summary>
        public void Wheel(int deltaX, int deltaY)
        {
            ((IJavaScriptExecutor)_driver)
                .ExecuteScript($"window.scrollBy({deltaX}, {deltaY});");
        }

        /// <summary>
        /// Scrolls inside <paramref name="element"/> by the given deltas via JS.
        /// </summary>
        public void Wheel(IWebElement element, int deltaX, int deltaY)
        {
            ((IJavaScriptExecutor)_driver)
                .ExecuteScript($"arguments[0].scrollBy({deltaX}, {deltaY});", element);
        }

        // ─── Helpers ──────────────────────────────────────────────────────────────

        private void MoveToAbsolute(int x, int y)
        {
            // Move from origin (top-left of the document) using JS scroll offset compensation
            ((IJavaScriptExecutor)_driver)
                .ExecuteScript($"window.scrollTo(0, 0);");
            new Actions(_driver)
                .MoveToElement(
                    _driver.FindElement(By.TagName("body")),
                    x, y)
                .Perform();
        }
    }
}
