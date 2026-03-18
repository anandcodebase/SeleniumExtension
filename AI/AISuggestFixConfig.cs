using SimpleSeleniumSupport.AI.Sanitization;

namespace SimpleSeleniumSupport.AI
{
    /// <summary>
    /// Per-process configuration for <see cref="AISuggestFixExtensions"/>.
    /// Set properties once at test-suite startup (e.g. <c>[OneTimeSetUp]</c>).
    /// </summary>
    public static class AISuggestFixConfig
    {
        /// <summary>Whether to send the trimmed DOM snapshot to the AI instead of full HTML. Default true.</summary>
        public static bool UseTrimmedDom { get; set; } = true;

        /// <summary>Max nodes collected for the trimmed DOM snapshot.</summary>
        public static int TrimMaxNodes { get; set; } = 400;

        /// <summary>Max characters of the trimmed DOM JSON sent to the AI.</summary>
        public static int TrimMaxChars { get; set; } = 10000;

        /// <summary>
        /// When <see langword="true"/>, sensitive data (passwords, usernames, file paths,
        /// API tokens) is scrubbed from the AI prompt before it is sent to the provider.
        /// <para>
        /// Default is <see langword="false"/> (opt-in). Set
        /// <see cref="SimpleSeleniumSupportDefaults.AiSanitizeByDefault"/> = <see langword="true"/>
        /// for a process-wide opt-in without touching each call site, or set this property
        /// directly to override that default for a specific test fixture.
        /// </para>
        /// </summary>
        public static bool SanitizeBeforeSend
        {
            get => _sanitize ?? SimpleSeleniumSupportDefaults.AiSanitizeByDefault;
            set => _sanitize = value;
        }
        private static bool? _sanitize;

        /// <summary>
        /// Fine-grained control over which categories of data are redacted.
        /// Only applied when <see cref="SanitizeBeforeSend"/> is <see langword="true"/>.
        /// </summary>
        public static SanitizationOptions SanitizationOptions { get; set; } = new();
    }
}
