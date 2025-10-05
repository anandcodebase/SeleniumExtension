

namespace SimpleSeleniumSupport
{
    public static class AISuggestFixConfig
    {
        /// <summary>Whether to send the trimmed DOM snapshot to the AI instead of full HTML. Default true.</summary>
        public static bool UseTrimmedDom { get; set; } = true;

        /// <summary>Max nodes collected for the trimmed DOM snapshot.</summary>
        public static int TrimMaxNodes { get; set; } = 400;

        /// <summary>Max characters of the trimmed DOM JSON sent to the AI.</summary>
        public static int TrimMaxChars { get; set; } = 10000;
    }
}
