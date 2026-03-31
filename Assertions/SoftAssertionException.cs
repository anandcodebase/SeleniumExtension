using System;
using System.Collections.Generic;
using System.Text;

namespace SimpleSeleniumSupport.Assertions
{
    /// <summary>
    /// Thrown by <see cref="SoftAssertions.AssertAll"/> (or automatically on <c>Dispose()</c>)
    /// when one or more soft assertions have failed. Contains all failures aggregated into one message.
    /// </summary>
    public sealed class SoftAssertionException : Exception
    {
        /// <summary>All individual assertion failures collected during the soft-assertion block.</summary>
        public IReadOnlyList<WebAssertionException> Failures { get; }

        internal SoftAssertionException(IReadOnlyList<WebAssertionException> failures)
            : base(BuildMessage(failures))
        {
            Failures = failures;
        }

        private static string BuildMessage(IReadOnlyList<WebAssertionException> failures)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine($"{failures.Count} soft assertion(s) failed:");
            for (int i = 0; i < failures.Count; i++)
                stringBuilder.AppendLine($"  [{i + 1}] {failures[i].Message}");
            return stringBuilder.ToString().TrimEnd();
        }
    }
}
