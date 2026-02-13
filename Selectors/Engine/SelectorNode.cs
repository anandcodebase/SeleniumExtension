using OpenQA.Selenium;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SimpleSeleniumSupport.Selectors.Engine
{
    public sealed class SelectorNode
    {
        public string LogicalName { get; }
        public IReadOnlyList<ISelectorStrategy> Strategies { get; }

        public SelectorNode(string logicalName, IEnumerable<ISelectorStrategy> strategies)
        {
            LogicalName = logicalName;
            Strategies = strategies.ToList().AsReadOnly();
        }

        public SelectorResult Resolve(ISearchContext context)
        {
            var results = new List<SelectorResult>();

            foreach (var strategy in Strategies)
            {
                IReadOnlyCollection<IWebElement> found;
                try { found = strategy.FindElements(context); }
                catch { continue; }

                foreach (var el in found)
                {
                    var confidence =
                        strategy.BaseConfidence +
                        (Safe(() => el.Displayed) ? 0.1 : 0) +
                        (Safe(() => el.Enabled) ? 0.05 : 0);

                    results.Add(new SelectorResult(
                        el,
                        Math.Min(1.0, confidence),
                        strategy.StrategyId));
                }
            }

            return results
                .OrderByDescending(r => r.Confidence)
                .FirstOrDefault();
        }

        private static bool Safe(Func<bool> fn)
        {
            try { return fn(); } catch { return false; }
        }
    }
}
