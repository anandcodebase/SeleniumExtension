using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace SimpleSeleniumSupport.AI.Providers
{
    /// <summary>
    /// <see cref="IAIProvider"/> implementation for the Cohere Chat API v2.
    /// Supports Command R, Command R+, and other Cohere chat models.
    /// <para>
    /// API reference: <c>POST https://api.cohere.com/v2/chat</c>
    /// </para>
    /// <example>
    /// <code>
    /// AIProviderRegistry.Use("cohere", new AIProviderConfig
    /// {
    ///     ApiKey = Environment.GetEnvironmentVariable("COHERE_API_KEY"),
    ///     Model  = "command-r-plus"   // or "command-r", "command-r7b-12-2024"
    /// });
    /// </code>
    /// </example>
    /// </summary>
    public sealed class CohereProvider : IAIProvider
    {
        private const string DefaultBaseUrl = "https://api.cohere.com/v2/chat";
        private const string DefaultModel   = "command-r-plus";

        private readonly string _baseUrl;
        private readonly string _defaultModel;
        private readonly int    _defaultTimeoutMs;
        private readonly string _apiKey;

        private static readonly Lazy<HttpClient> _httpClient = new(() => new HttpClient());

        /// <inheritdoc/>
        public string ProviderId => "cohere";

        /// <summary>Creates a Cohere provider with the given configuration.</summary>
        /// <exception cref="ArgumentException">Thrown when <see cref="AIProviderConfig.ApiKey"/> is missing.</exception>
        public CohereProvider(AIProviderConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.ApiKey))
                throw new ArgumentException("Cohere requires an API key. Set AIProviderConfig.ApiKey.", nameof(config));

            _apiKey           = config.ApiKey;
            _baseUrl          = config.BaseUrl ?? DefaultBaseUrl;
            _defaultModel     = config.Model   ?? DefaultModel;
            _defaultTimeoutMs = config.TimeoutMs;
        }

        /// <inheritdoc/>
        public async Task<AIResponse> GenerateAsync(
            string            prompt,
            AIRequestOptions  options,
            CancellationToken cancellationToken = default)
        {
            var model     = options.Model     ?? _defaultModel;
            var timeoutMs = options.TimeoutMs ?? _defaultTimeoutMs;
            var sw        = Stopwatch.StartNew();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            try
            {
                var payload = new
                {
                    model,
                    messages    = new[] { new { role = "user", content = prompt } },
                    max_tokens  = options.MaxTokens  ?? 2048,
                    temperature = (float)(options.Temperature ?? 0.2)
                };

                var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };
                request.Headers.Add("Authorization", $"Bearer {_apiKey}");

                var response = await _httpClient.Value
                    .SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                return ParseResponse(body, sw.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return AIResponse.Fail($"Cohere request timed out after {timeoutMs}ms.", ProviderId, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                return AIResponse.Fail($"Cohere error: {ex.Message}", ProviderId, sw.ElapsedMilliseconds);
            }
        }

        private AIResponse ParseResponse(string body, long latencyMs)
        {
            try
            {
                using var doc  = JsonDocument.Parse(body);
                var root = doc.RootElement;

                // Error response: { "message": "...", "status": 401 }  (top-level string "message")
                if (root.TryGetProperty("message", out var topMsg) &&
                    topMsg.ValueKind == JsonValueKind.String &&
                    root.TryGetProperty("status", out var status) &&
                    status.ValueKind == JsonValueKind.Number)
                {
                    return AIResponse.Fail(topMsg.GetString() ?? "Cohere error", ProviderId, latencyMs);
                }

                // v2 success response: { "message": { "role": "assistant", "content": [{ "type": "text", "text": "..." }] } }
                var text = root
                    .GetProperty("message")
                    .GetProperty("content")[0]
                    .GetProperty("text")
                    .GetString()?.Trim() ?? "";

                AITokenUsage? usage = null;
                if (root.TryGetProperty("usage", out var u))
                {
                    // Prefer billed_units; fall back to tokens
                    var src = u.TryGetProperty("billed_units", out var bu) ? bu : u;
                    var inp  = src.TryGetProperty("input_tokens",  out var i) ? i.GetInt32() : 0;
                    var out_ = src.TryGetProperty("output_tokens", out var o) ? o.GetInt32() : 0;
                    usage = new AITokenUsage { PromptTokens = inp, ResponseTokens = out_, TotalTokens = inp + out_ };
                }

                return AIResponse.Ok(text, ProviderId, latencyMs, usage);
            }
            catch (Exception ex)
            {
                return AIResponse.Fail($"Cohere response parse error: {ex.Message}", ProviderId, latencyMs);
            }
        }
    }
}
