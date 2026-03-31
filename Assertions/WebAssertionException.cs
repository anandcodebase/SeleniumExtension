using System;

namespace SimpleSeleniumSupport.Assertions
{
    /// <summary>
    /// Thrown when a <see cref="LocatorAssertion"/> or <see cref="PageAssertion"/> check fails.
    /// Contains the locator description, the expected value, and the actual value to aid diagnosis.
    /// </summary>
    public class WebAssertionException : Exception
    {
        /// <summary>Human-readable description of the locator / target being asserted on.</summary>
        public string Target { get; }
        /// <summary>The expected value as a string (may be null for boolean assertions).</summary>
        public string? Expected { get; }
        /// <summary>The actual value observed at assertion time.</summary>
        public string? Actual { get; }

        /// <summary>
        /// Initialises a new <see cref="WebAssertionException"/> with a target, message, and optional
        /// expected/actual values.
        /// </summary>
        public WebAssertionException(string target, string message, string? expected = null, string? actual = null)
            : base(message)
        {
            Target = target;
            Expected = expected;
            Actual = actual;
        }
    }
}
