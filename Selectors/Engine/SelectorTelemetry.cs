using System.Collections.Concurrent;
using System.Collections.Generic;

namespace SimpleSeleniumSupport.Selectors.Engine
{
    public static class SelectorTelemetry
    {
        private static readonly ConcurrentBag<object> _events = new();

        public static void Record(object ev) => _events.Add(ev);

        public static IReadOnlyCollection<object> Snapshot()
            => _events.ToArray();
        public static void Clear()
        {
            while (_events.TryTake(out _)) { }
        }
    }
}
