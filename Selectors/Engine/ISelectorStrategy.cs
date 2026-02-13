using OpenQA.Selenium;
using System.Collections.Generic;

namespace SimpleSeleniumSupport.Selectors.Engine
{
    public interface ISelectorStrategy
    {
        string StrategyId { get; }
        string StrategyType { get; }
        double BaseConfidence { get; }

        IReadOnlyCollection<IWebElement> FindElements(ISearchContext context);
    }
}
