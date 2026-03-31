using System;
using System.Text.RegularExpressions;

namespace SimpleSeleniumSupport.Network
{
    /// <summary>
    /// Converts a Playwright-style glob URL pattern to a <see cref="Regex"/>.
    /// <list type="bullet">
    ///   <item><c>**</c> — matches any sequence of characters including <c>/</c></item>
    ///   <item><c>*</c>  — matches any sequence of characters excluding <c>/</c></item>
    ///   <item><c>?</c>  — matches exactly one character except <c>/</c></item>
    /// </list>
    /// </summary>
    internal static class RoutePattern
    {
        /// <summary>
        /// Returns a <see cref="Regex"/> that matches URLs according to the glob <paramref name="pattern"/>.
        /// If <paramref name="pattern"/> already starts with <c>^</c>, it is treated as a raw regex.
        /// </summary>
        internal static Regex ToRegex(string pattern)
        {
            if (pattern == null) throw new ArgumentNullException(nameof(pattern));

            // Raw regex passthrough
            if (pattern.StartsWith("^"))
                return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);

            // Glob → regex conversion
            var stringBuilder = new System.Text.StringBuilder("^");
            int i = 0;
            while (i < pattern.Length)
            {
                if (i + 1 < pattern.Length && pattern[i] == '*' && pattern[i + 1] == '*')
                {
                    stringBuilder.Append(".*");
                    i += 2;
                    // consume optional leading slash after **
                    if (i < pattern.Length && pattern[i] == '/') i++;
                }
                else if (pattern[i] == '*')
                {
                    stringBuilder.Append("[^/]*");
                    i++;
                }
                else if (pattern[i] == '?')
                {
                    stringBuilder.Append("[^/]");
                    i++;
                }
                else
                {
                    stringBuilder.Append(Regex.Escape(pattern[i].ToString()));
                    i++;
                }
            }
            stringBuilder.Append("$");

            return new Regex(stringBuilder.ToString(), RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }

        /// <summary>Returns <c>true</c> if <paramref name="url"/> matches <paramref name="pattern"/>.</summary>
        internal static bool IsMatch(string pattern, string url)
        {
            try { return ToRegex(pattern).IsMatch(url); }
            catch { return false; }
        }
    }
}
