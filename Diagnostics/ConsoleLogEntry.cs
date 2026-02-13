

namespace SimpleSeleniumSupport.Diagnostics
{
    /// <summary>
    /// Represents a single entry from the browser's console log.
    /// </summary>
    public class ConsoleLogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
        public string Source { get; set; }
        public string Text { get; set; }
        public string Url { get; set; }
        public long? LineNumber { get; set; }
    }
}