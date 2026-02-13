using System.Linq;

namespace SimpleSeleniumSupport.Selectors
{
    /// <summary>
    /// Shared XPath string-quoting utilities.
    /// </summary>
    internal static class XPathHelper
    {
        /// <summary>
        /// Properly quotes a string literal for XPath usage, handling single quotes,
        /// double quotes, and mixed-quote values via concat().
        /// </summary>
        public static string Quote(string value)
        {
            if (value == null) return "''";
            if (!value.Contains("'")) return $"'{value}'";
            if (!value.Contains("\"")) return $"\"{value}\"";
            var parts = value.Split('\'');
            return "concat(" + string.Join(", \"'\", ", parts.Select(p => $"'{p}'")) + ")";
        }

        /// <summary>
        /// Returns a lowercased, single-quoted literal for use inside
        /// translate(…, 'ABC…', 'abc…') XPath expressions.
        /// </summary>
        public static string QuotePartial(string value)
        {
            if (value == null) value = "";
            return $"'{value.ToLowerInvariant().Replace("'", "\\'")}'";
        }
    }
}
