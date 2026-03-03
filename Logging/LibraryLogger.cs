using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;

namespace SimpleSeleniumSupport.Logging
{
    /// <summary>
    /// Internal logging shim for SimpleSeleniumSupport.
    /// <para>
    /// By default uses <see cref="NullLoggerFactory"/> (silent). Wire a real
    /// <see cref="ILoggerFactory"/> once at suite startup via
    /// <see cref="SimpleSeleniumSupportDefaults.LoggerFactory"/>:
    /// </para>
    /// <code>
    /// SimpleSeleniumSupportDefaults.LoggerFactory = LoggerFactory.Create(b =>
    ///     b.AddConsole().SetMinimumLevel(LogLevel.Debug));
    /// </code>
    /// </summary>
    internal static class LibraryLogger
    {
        private static ILoggerFactory _factory = NullLoggerFactory.Instance;
        private static readonly ConcurrentDictionary<string, ILogger> _cache = new();

        /// <summary>Replaces the active factory. Called by <see cref="SimpleSeleniumSupportDefaults"/>.</summary>
        internal static void Configure(ILoggerFactory factory)
        {
            _factory = factory ?? NullLoggerFactory.Instance;
            _cache.Clear();
        }

        /// <summary>Returns a cached <see cref="ILogger"/> scoped to type <typeparamref name="T"/>.</summary>
        internal static ILogger For<T>()
            => _cache.GetOrAdd(
                typeof(T).FullName ?? typeof(T).Name,
                k => _factory.CreateLogger(k));

        /// <summary>Returns a cached <see cref="ILogger"/> scoped to the given category string.</summary>
        internal static ILogger ForCategory(string category)
            => _cache.GetOrAdd(category, k => _factory.CreateLogger(k));
    }
}
