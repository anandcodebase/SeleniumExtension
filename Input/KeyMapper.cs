using OpenQA.Selenium;
using System.Collections.Generic;

namespace SimpleSeleniumSupport.Input
{
    /// <summary>
    /// Maps Playwright / human-readable key names to Selenium <see cref="Keys"/> constants.
    /// Also parses compound modifiers like <c>Control+A</c>, <c>Shift+Enter</c>.
    /// </summary>
    public static class KeyMapper
    {
        private static readonly Dictionary<string, string> _map =
            new Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
        {
            ["Enter"]      = Keys.Enter,
            ["Return"]     = Keys.Return,
            ["Tab"]        = Keys.Tab,
            ["Escape"]     = Keys.Escape,
            ["Esc"]        = Keys.Escape,
            ["Backspace"]  = Keys.Backspace,
            ["Delete"]     = Keys.Delete,
            ["Space"]      = Keys.Space,
            [" "]          = Keys.Space,
            ["ArrowUp"]    = Keys.ArrowUp,
            ["Up"]         = Keys.ArrowUp,
            ["ArrowDown"]  = Keys.ArrowDown,
            ["Down"]       = Keys.ArrowDown,
            ["ArrowLeft"]  = Keys.ArrowLeft,
            ["Left"]       = Keys.ArrowLeft,
            ["ArrowRight"] = Keys.ArrowRight,
            ["Right"]      = Keys.ArrowRight,
            ["Home"]       = Keys.Home,
            ["End"]        = Keys.End,
            ["PageUp"]     = Keys.PageUp,
            ["PageDown"]   = Keys.PageDown,
            ["Insert"]     = Keys.Insert,
            ["F1"]         = Keys.F1,
            ["F2"]         = Keys.F2,
            ["F3"]         = Keys.F3,
            ["F4"]         = Keys.F4,
            ["F5"]         = Keys.F5,
            ["F6"]         = Keys.F6,
            ["F7"]         = Keys.F7,
            ["F8"]         = Keys.F8,
            ["F9"]         = Keys.F9,
            ["F10"]        = Keys.F10,
            ["F11"]        = Keys.F11,
            ["F12"]        = Keys.F12,
            ["Control"]    = Keys.Control,
            ["Ctrl"]       = Keys.Control,
            ["Shift"]      = Keys.Shift,
            ["Alt"]        = Keys.Alt,
            ["Meta"]       = Keys.Meta,
            ["Command"]    = Keys.Command,
            ["Null"]       = Keys.Null,
        };

        /// <summary>
        /// Converts a key name or compound like <c>Control+A</c> to the Selenium key sequence.
        /// </summary>
        public static string Map(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;

            // Check compound modifier (e.g. "Control+A", "Shift+Enter")
            var plusIndex = key.IndexOf('+');
            if (plusIndex > 0)
            {
                var modifier = key[..plusIndex];
                var rest = key[(plusIndex + 1)..];
                if (_map.TryGetValue(modifier, out var modifierKey))
                    return modifierKey + Map(rest) + modifierKey;
            }

            return _map.TryGetValue(key, out var mapped) ? mapped : key;
        }
    }
}
