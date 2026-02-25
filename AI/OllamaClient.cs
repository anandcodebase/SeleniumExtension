
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
        public static string ApiUrl { get; set; } = "http://localhost:11434/api/generate";

        /// <summary>
        /// Timeout for Ollama requests (ms). Default 30s.
        /// </summary>
        /// <value>
        /// The timeout ms.
        /// </value>
        public static int TimeoutMs { get; set; } = 30000;

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
        /// Generates the specified prompt.
        /// </summary>
        /// <param name="prompt">The prompt.</param>
        /// <param name="model">The model.</param>
        /// <returns></returns>
        /// <exception cref="System.Net.Http.HttpRequestException">
        /// Ollama request timed out after {TimeoutMs}ms.
        /// or
        /// Could not connect to Ollama at {ApiUrl}. Is Ollama running? Details: {ex.Message}
        /// </exception>
        /// <exception cref="System.InvalidOperationException">An unexpected error occurred during AI generation: {ex.Message}</exception>
        public static string Generate(string prompt, string model)
        {
            if (MockMode)
            {
                Console.WriteLine("[OllamaClient] MockMode is ON - returning placeholder response.");
                return "//body"; // very simple mock
            }

            try
            {
                using var cts = new CancellationTokenSource(TimeoutMs);
                var payload = new
                {
                    model,
                    prompt,
                    stream = false
                };

                string jsonPayload = JsonSerializer.Serialize(payload);
                var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl)
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };

                ConfigureRequest?.Invoke(request);

                var task = _httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token);
                task.Wait(cts.Token);
                var response = task.Result;
                response.EnsureSuccessStatusCode();

                var readTask = response.Content.ReadAsStringAsync();
                readTask.Wait(cts.Token);
                var responseBody = readTask.Result;

                // Try to parse several common shapes: { "response": "..."} or { "outputs":[{"content":"..."}]} or raw string.
                try
                {
                    using var doc = JsonDocument.Parse(responseBody);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("response", out var r) && r.ValueKind == JsonValueKind.String)
                        return r.GetString()!.Trim();

                    if (root.TryGetProperty("outputs", out var outputs) && outputs.ValueKind == JsonValueKind.Array && outputs.GetArrayLength() > 0)
                    {
                        var first = outputs[0];
                        if (first.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                            return content.GetString()!.Trim();

                        // Some versions give an "text" property
                        if (first.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String)
                            return text.GetString()!.Trim();
                    }

                    // Fallback: if top-level is string
                    if (root.ValueKind == JsonValueKind.String)
                        return root.GetString()!.Trim();
                }
                catch (JsonException)
                {
                    // Not JSON or unexpected shape — fall through to return raw body
                }

                return responseBody.Trim();
            }
            catch (AggregateException ae) when (ae.InnerException is OperationCanceledException)
            {
                throw new HttpRequestException($"Ollama request timed out after {TimeoutMs}ms.", ae);
            }
            catch (HttpRequestException ex)
            {
                throw new HttpRequestException($"Could not connect to Ollama at {ApiUrl}. Is Ollama running? Details: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"An unexpected error occurred during AI generation: {ex.Message}", ex);
            }
        }
    }
}
