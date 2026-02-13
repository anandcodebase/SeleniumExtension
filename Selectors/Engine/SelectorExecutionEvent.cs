using System;

namespace SimpleSeleniumSupport.Selectors.Engine
{
    public sealed class SelectorExecutionEvent
    {
        public string SelectorId { get; set; }
        public string StrategyId { get; set; }
        public double Confidence { get; set; }
        public bool Success { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
