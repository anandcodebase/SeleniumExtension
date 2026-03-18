using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SimpleSeleniumSupport.AI.Sanitization
{
    /// <summary>
    /// Controls which categories of sensitive data are redacted from AI prompts
    /// and report fields before they leave the test process.
    /// </summary>
    public sealed class SanitizationOptions
    {
        // ── Toggles ───────────────────────────────────────────────────────────

        /// <summary>Redact password= / pwd= / secret= style key-value pairs.</summary>
        public bool RedactPasswords { get; set; } = true;

        /// <summary>Redact username= / user= / login= style key-value pairs.</summary>
        public bool RedactUsernames { get; set; } = true;

        /// <summary>Redact email addresses.</summary>
        public bool RedactEmails { get; set; } = true;

        /// <summary>Redact Bearer tokens and api_key= style values.</summary>
        public bool RedactApiTokens { get; set; } = true;

        /// <summary>
        /// Redact file system paths that reveal the user's codebase location
        /// (Windows C:\Users\... and Unix /home/... or /Users/...).
        /// </summary>
        public bool RedactFilePaths { get; set; } = true;

        /// <summary>Redact http://user:pass@host style URL credentials.</summary>
        public bool RedactUrlCredentials { get; set; } = true;

        /// <summary>
        /// When sanitizing a DOM JSON snapshot from DomTrimmer, blank the <c>text</c>
        /// of input nodes whose type is "password" or whose name matches common credential
        /// field names (password, passwd, secret, token, etc.).
        /// </summary>
        public bool RedactDomInputValues { get; set; } = true;

        // ── Replacement tokens ────────────────────────────────────────────────

        /// <summary>Token substituted in place of a redacted password value.</summary>
        public string PasswordToken { get; set; } = "[REDACTED_PASSWORD]";

        /// <summary>Token substituted in place of a redacted username value.</summary>
        public string UsernameToken { get; set; } = "[REDACTED_USERNAME]";

        /// <summary>Token substituted in place of a redacted email address.</summary>
        public string EmailToken    { get; set; } = "[REDACTED_EMAIL]";

        /// <summary>Token substituted in place of a redacted API key or Bearer token.</summary>
        public string ApiTokenToken { get; set; } = "[REDACTED_TOKEN]";

        /// <summary>Token substituted in place of a redacted file system path.</summary>
        public string PathToken     { get; set; } = "[REDACTED_PATH]";

        // ── Custom rules ──────────────────────────────────────────────────────

        /// <summary>
        /// Additional patterns to redact. Applied after all built-in rules.
        /// Supply a compiled <see cref="Regex"/> and the replacement string.
        /// </summary>
        public IList<(Regex Pattern, string Replacement)> CustomRules { get; set; }
            = new List<(Regex, string)>();
    }
}
