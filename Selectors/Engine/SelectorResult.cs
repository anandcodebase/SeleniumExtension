using OpenQA.Selenium;

namespace SimpleSeleniumSupport.Selectors.Engine
{
    public sealed class SelectorResult
    {
        public IWebElement Element { get; }
        public double Confidence { get; }
        public string StrategyId { get; }

        public SelectorResult(IWebElement element, double confidence, string strategyId)
        {
            Element = element;
            Confidence = confidence;
            StrategyId = strategyId;
        }
    }
}
