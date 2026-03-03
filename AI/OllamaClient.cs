
using Microsoft.Extensions.Logging;
using SimpleSeleniumSupport.Logging;
using System.Text;
using System.Text.Json;


namespace SimpleSeleniumSupport.AI
{
    /// <summary>
    /// Ollama Client
    /// </summary>
    internal static class OllamaClient
    {
        private static readonly ILogger _log = LibraryLogger.ForCategory("SimpleSeleniumSupport.AI.OllamaClient");

        /// <summary>
        /// The HTTP client
        /// </summary>
        private static readonly HttpClient _httpClient = new HttpClient();
        /// <summary>
        /// The lock
        /// </summary>
        private static readonly object _lock = new();

        /// <summary>
        /// API endpoint (change if your Ollama listens somewhere else)
        /// </summary>
        /// <value>
        /// The API URL.
        /// </value>
        public static string ApiUrl { get; set; } = SimpleSeleniumSupportDefaults.OllamaBaseUrl;

        /// <summary>
        /// Timeout for Ollama requests (ms). Default 30s.
        /// </summary>
        /// <value>
        /// The timeout ms.
        /// </value>
        public static int TimeoutMs { get; set; } = SimpleSeleniumSupportDefaults.OllamaTimeoutMs;

        /// <summary>
        /// If true, the client will return a mocked response for testing.
        /// </summary>
        /// <value>
        ///   <c>true</c> if [mock mode]; otherwise, <c>false</c>.
        /// </value>
        public static bool MockMode { get; set; } = false;

        /// <summary>
        /// Optional extra headers (e.g., Authorization) to include in requests.
        /// </summary>
        /// <value>
        /// The configure request.
        /// </value>
        public static Action<HttpRequestMessage>? ConfigureRequest { get; set; }

        /// <summary>
        /// Generates the specified prompt using the currently registered <see cref="AIProviderRegistry.Current"/> provider.
        /// Falls back to direct Ollama HTTP if mock mode is on.
        /// </summary>
        /// <param name="prompt">The prompt.</param>
        /// <param name="model">Model name override. Passed to the active provider.</param>
        /// <returns>The model's text response.</returns>
        public static string Generate(string prompt, string model)
            => GenerateAsync(prompt, model, CancellationToken.None).GetAwaiter().GetResult();

        /// <summary>
        /// Async version of <see cref="Generate"/>. Routes through <see cref="AIProviderRegistry.Current"/>.
        /// </summary>
        public static async Task<string> GenerateAsync(
            string prompt, string model, CancellationToken cancellationToken = default)
        {
            if (MockMode)
            {
                _log.LogDebug("[OllamaClient] MockMode is ON — returning placeholder response.");
                return "//body";
            }

            var opts = new AIRequestOptions { Model = model, TimeoutMs = TimeoutMs };
            var result = await AIProviderRegistry.Current
                .GenerateAsync(prompt, opts, cancellationToken)
                .ConfigureAwait(false);

            if (!result.Success)
                throw new HttpRequestException(result.ErrorMessage ?? "AI provider returned an error.");

            return result.Text;
        }
    }
}
