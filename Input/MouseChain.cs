using OpenQA.Selenium;
using OpenQA.Selenium.Interactions;

namespace SimpleSeleniumSupport.Input
{
    /// <summary>
    /// Fluent builder for chained mouse operations (move, down, up, wheel).
    /// Call <see cref="Perform"/> to commit the entire chain.
    /// Created by <see cref="SeleniumMouse.Down()"/> for drag-style interactions.
    /// </summary>
    public sealed class MouseChain
    {
        private readonly Actions _actions;
        private readonly IWebDriver _driver;

        internal MouseChain(IWebDriver driver, Actions actions)
        {
            _driver = driver;
            _actions = actions;
        }

        /// <summary>Adds a mouse-move to the absolute coordinates (<paramref name="x"/>, <paramref name="y"/>).</summary>
        public MouseChain Move(int x, int y)
        {
            _actions.MoveByOffset(x, y);
            return this;
        }

        /// <summary>Adds a mouse-move to the element's centre.</summary>
        public MouseChain MoveTo(IWebElement element)
        {
            _actions.MoveToElement(element);
            return this;
        }

        /// <summary>Releases the left mouse button.</summary>
        public MouseChain Up()
        {
            _actions.Release();
            return this;
        }

        /// <summary>Executes the accumulated chain of actions.</summary>
        public void Perform() => _actions.Perform();
    }
}
