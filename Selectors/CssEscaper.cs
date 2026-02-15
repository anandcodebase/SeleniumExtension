namespace SimpleSeleniumSupport.Selectors
{
    /// <summary>
    /// CSS value escaping utilities for use in attribute selectors.
    /// </summary>
    internal static class CssEscaper
    {
        /// <summary>
        /// Escapes a value for safe use inside a single-quoted CSS attribute selector.
        /// Example: [data-testid='&lt;escaped_value&gt;']
        /// </summary>
        public static string EscapeAttributeValue(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value
                .Replace("\\", "\\\\")
                .Replace("\0", "\\0 ")
                .Replace("'", "\\'");
        }
    }
}
