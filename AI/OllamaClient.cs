
using System.Text;
using System.Text.Json;


namespace SimpleSeleniumSupport.AI
{
    /// <summary>
    /// Ollama Client
    /// </summary>
    internal static class OllamaClient
    {
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
        public static Action<HttpRequestMessage> ConfigureRequest { get; set; }

        /// <summary>
        /// Generates the specified prompt (synchronous wrapper around <see cref="GenerateAsync"/>).
        /// </summary>
        /// <param name="prompt">The prompt.</param>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        public static string Generate(string prompt, string model)
            => GenerateAsync(prompt, model, CancellationToken.None).GetAwaiter().GetResult();

        /// <summary>
        /// Sends <paramref name="prompt"/> to Ollama and returns the model's text response.
        /// Suitable for use in async test frameworks (xUnit, NUnit 3) without
        /// deadlock-prone <c>.Result</c> / <c>.GetAwaiter().GetResult()</c> calls.
        /// </summary>
        /// <param name="prompt">The prompt.</param>
        /// <param name="model">The Ollama model name (e.g. "llama3").</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The model's text response.</returns>
        /// <exception cref="System.Net.Http.HttpRequestException">
        /// Thrown when Ollama is unreachable or the request times out.
        /// </exception>
        /// <exception cref="System.InvalidOperationException">
        /// Thrown on unexpected errors during generation.
        /// </exception>
        public static async Task<string> GenerateAsync(
            string prompt, string model, CancellationToken cancellationToken = default)
        {
            if (MockMode)
            {
                Console.WriteLine("[OllamaClient] MockMode is ON - returning placeholder response.");
                return "//body";
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeoutMs);

            try
            {
                var payload = new { model, prompt, stream = false };
                string jsonPayload = JsonSerializer.Serialize(payload);

                var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };
                ConfigureRequest?.Invoke(request);

                var response = await _httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content
                    .ReadAsStringAsync(cts.Token)
                    .ConfigureAwait(false);

                return ParseResponse(responseBody);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                throw new HttpRequestException($"Ollama request timed out after {TimeoutMs}ms.");
            }
            catch (HttpRequestException ex)
            {
                throw new HttpRequestException(
                    $"Could not connect to Ollama at {ApiUrl}. Is Ollama running? Details: {ex.Message}", ex);
            }
            catch (Exception ex) when (ex is not HttpRequestException)
            {
                throw new InvalidOperationException(
                    $"An unexpected error occurred during AI generation: {ex.Message}", ex);
            }
        }

        // Parses several common Ollama response shapes.
        private static string ParseResponse(string responseBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                if (root.TryGetProperty("response", out var r) && r.ValueKind == JsonValueKind.String)
                    return r.GetString()!.Trim();

                if (root.TryGetProperty("outputs", out var outputs) &&
                    outputs.ValueKind == JsonValueKind.Array &&
                    outputs.GetArrayLength() > 0)
                {
                    var first = outputs[0];
                    if (first.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                        return content.GetString()!.Trim();
                    if (first.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                        return text.GetString()!.Trim();
                }

                if (root.ValueKind == JsonValueKind.String)
                    return root.GetString()!.Trim();
            }
            catch (JsonException) { /* fall through to return raw body */ }

            return responseBody.Trim();
        }
    }
}
