using OpenQA.Selenium;
using System.Collections.Generic;

namespace SimpleSeleniumSupport.Selectors.Engine
{
    public sealed class SelectorPath
    {
        public string PathId { get; }
        public IReadOnlyList<SelectorNode> Nodes { get; }

        public SelectorPath(string pathId, IEnumerable<SelectorNode> nodes)
        {
            PathId = pathId;
            Nodes = new List<SelectorNode>(nodes).AsReadOnly();
        }

        public SelectorResult? Resolve(IWebDriver driver)
        {
            ISearchContext context = driver;
            SelectorResult? last = null;

            foreach (var node in Nodes)
            {
                last = node.Resolve(context);
                if (last == null)
                    return null;

                context = last.Element;
            }

            return last;
        }
    }
}
