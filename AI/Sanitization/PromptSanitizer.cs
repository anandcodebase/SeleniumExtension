using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace SimpleSeleniumSupport.AI.Sanitization
{
    /// <summary>
    /// Scrubs sensitive data from AI prompt strings and DOM JSON snapshots
    /// produced by <see cref="DomTrimmer"/>.
    /// All methods are pure — they return new strings and never mutate the input.
    /// </summary>
    public static class PromptSanitizer
    {
        // ── Built-in regex patterns ───────────────────────────────────────────

        // http://user:pass@host  or  https://user:pass@host
        private static readonly Regex RxUrlCreds =
            new(@"(https?://)([^:@\s]+:[^@\s]+@)",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // password=xxx  passwd:xxx  pwd=xxx  secret=xxx  pass=xxx
        private static readonly Regex RxPassword =
            new(@"(password|passwd|pwd|secret|pass)\s*[=:]\s*\S+",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // username=xxx  user=xxx  username:xxx  login=xxx  uid=xxx
        private static readonly Regex RxUsername =
            new(@"(username|user(?:name)?|login|uid)\s*[=:]\s*\S+",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // user@domain.ext
        private static readonly Regex RxEmail =
            new(@"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}",
                RegexOptions.Compiled);

        // Bearer eyJhbGci...
        private static readonly Regex RxBearerToken =
            new(@"Bearer\s+\S+", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // api_key=xxx  apikey=xxx  access_token=xxx  access-token=xxx
        private static readonly Regex RxApiKey =
            new(@"(api[_\-]?key|apikey|access[_\-]?token)\s*[=:]\s*\S+",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // Windows: C:\Users\john\  or  C:\home\john\
        private static readonly Regex RxWinPath =
            new(@"[A-Za-z]:\\(?:Users|home)\\[^\\]+\\",
                RegexOptions.Compiled);

        // Unix: /home/john/  or  /Users/john/
        private static readonly Regex RxUnixPath =
            new(@"/(home|Users)/[^/\s]+/",
                RegexOptions.Compiled);

        // Credential-related field names for DOM input scrubbing
        private static readonly HashSet<string> CredentialFieldNames =
            new(StringComparer.OrdinalIgnoreCase)
            { "password", "passwd", "pwd", "secret", "token", "apikey", "api_key", "pass" };

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Returns a copy of <paramref name="input"/> with all enabled sensitive data
        /// patterns replaced by their corresponding redaction tokens.
        /// </summary>
        /// <param name="input">The raw text to sanitize (prompt, error message, stack trace, etc.).</param>
        /// <param name="opts">Which categories to redact; null = all defaults enabled.</param>
        public static string Sanitize(string input, SanitizationOptions? opts = null)
        {
            if (string.IsNullOrEmpty(input)) return input;
            opts ??= new SanitizationOptions();

            string s = input;

            if (opts.RedactUrlCredentials)
                s = RxUrlCreds.Replace(s, m =>
                    m.Groups[1].Value + $"{opts.UsernameToken}:{opts.PasswordToken}@");

            if (opts.RedactPasswords)
                s = RxPassword.Replace(s, m =>
                {
                    // Preserve the key name, replace only the value portion
                    var key = m.Value.Split(new[] { '=', ':' }, 2)[0];
                    return $"{key}={opts.PasswordToken}";
                });

            if (opts.RedactUsernames)
                s = RxUsername.Replace(s, m =>
                {
                    var key = m.Value.Split(new[] { '=', ':' }, 2)[0];
                    return $"{key}={opts.UsernameToken}";
                });

            if (opts.RedactApiTokens)
            {
                s = RxBearerToken.Replace(s, $"Bearer {opts.ApiTokenToken}");
                s = RxApiKey.Replace(s, m =>
                {
                    var key = m.Value.Split(new[] { '=', ':' }, 2)[0];
                    return $"{key}={opts.ApiTokenToken}";
                });
            }

            // Emails after tokens so user@domain is not confused with a key-value pair
            if (opts.RedactEmails)
                s = RxEmail.Replace(s, opts.EmailToken);

            if (opts.RedactFilePaths)
            {
                s = RxWinPath.Replace(s, opts.PathToken + @"\");
                s = RxUnixPath.Replace(s, "/" + opts.PathToken + "/");
            }

            foreach (var (pattern, replacement) in opts.CustomRules)
                s = pattern.Replace(s, replacement);

            return s;
        }

        /// <summary>
        /// Sanitizes the JSON array string produced by <see cref="DomTrimmer.TrimDom"/>.
        /// Replaces the <c>text</c> field of password input nodes with the password token.
        /// Returns <paramref name="domJson"/> unchanged if parsing fails (fail-safe).
        /// </summary>
        /// <param name="domJson">JSON string from DomTrimmer.</param>
        /// <param name="opts">Sanitization options; null = defaults.</param>
        public static string SanitizeDomJson(string domJson, SanitizationOptions? opts = null)
        {
            if (string.IsNullOrWhiteSpace(domJson)) return domJson;
            opts ??= new SanitizationOptions();
            if (!opts.RedactDomInputValues) return domJson;

            try
            {
                var nodes = JsonNode.Parse(domJson) as JsonArray;
                if (nodes == null) return domJson;

                foreach (var node in nodes)
                {
                    if (node == null) continue;
                    if (node["tag"]?.GetValue<string>() != "input") continue;

                    var attrs = node["attrs"] as JsonObject;
                    var type  = attrs?["type"]?.GetValue<string>() ?? "";
                    var name  = attrs?["name"]?.GetValue<string>() ?? "";

                    bool isSensitive =
                        string.Equals(type, "password", StringComparison.OrdinalIgnoreCase) ||
                        CredentialFieldNames.Contains(name);

                    if (isSensitive && node is JsonObject obj)
                        obj["text"] = opts.PasswordToken;
                }

                return nodes.ToJsonString();
            }
            catch
            {
                return domJson; // fail-safe: never break the caller
            }
        }
    }
}
