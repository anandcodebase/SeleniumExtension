using OpenQA.Selenium;
using System.Collections.Generic;

namespace SimpleSeleniumSupport.Selectors.Engine
{
    /// <summary>
    /// Wraps Selenium's native By locator as a selector strategy.
    /// </summary>
    public sealed class SeleniumByStrategy : ISelectorStrategy
    {
        private readonly OpenQA.Selenium.By _by;

        public SeleniumByStrategy(
            OpenQA.Selenium.By by,
            double baseConfidence = 0.7)
        {
            _by = by;
            BaseConfidence = baseConfidence;
            StrategyId = by.ToString();
        }

        public string StrategyId { get; }
        public string StrategyType => "Selenium.By";
        public double BaseConfidence { get; }

        public IReadOnlyCollection<IWebElement> FindElements(
            ISearchContext context)
        {
            return context.FindElements(_by);
        }
    }
}
