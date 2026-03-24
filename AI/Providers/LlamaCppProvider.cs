using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace SimpleSeleniumSupport.AI.Providers
{
    /// <summary>
    /// <see cref="IAIProvider"/> implementation for a <c>llama.cpp</c> HTTP server.
    /// <para>
    /// Targets the <c>POST /completion</c> endpoint exposed by
    /// <c>llama-server</c> / <c>llama.cpp</c> (distinct from the OpenAI-compatible
    /// <c>/v1/chat/completions</c> endpoint).
    /// </para>
    /// <para>
    /// Start the server: <c>llama-server -m model.gguf --port 8081</c>
    /// </para>
    /// <example>
    /// <code>
    /// AIProviderRegistry.Use("llama-cpp", new AIProviderConfig
    /// {
    ///     BaseUrl   = "http://localhost:8081/completion",   // default when omitted
    ///     TimeoutMs = 120_000
    /// });
    /// </code>
    /// </example>
    /// </summary>
    public sealed class LlamaCppProvider : IAIProvider
    {
        private const string DefaultBaseUrl = "http://localhost:8081/completion";

        private readonly string _baseUrl;
        private readonly int    _defaultTimeoutMs;

        private static readonly Lazy<HttpClient> _httpClient = new(() => new HttpClient());

        /// <inheritdoc/>
        public string ProviderId => "llama-cpp";

        /// <summary>Creates a llama.cpp provider with the given configuration.</summary>
        public LlamaCppProvider(AIProviderConfig config)
        {
            _baseUrl          = config.BaseUrl ?? DefaultBaseUrl;
            _defaultTimeoutMs = config.TimeoutMs;
        }

        /// <inheritdoc/>
        public async Task<AIResponse> GenerateAsync(
            string            prompt,
            AIRequestOptions  options,
            CancellationToken cancellationToken = default)
        {
            var timeoutMs = options.TimeoutMs ?? _defaultTimeoutMs;
            var sw        = Stopwatch.StartNew();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            try
            {
                var payload = new
                {
                    prompt,
                    n_predict   = options.MaxTokens  ?? 2048,
                    temperature = (float)(options.Temperature ?? 0.2),
                    stream      = false
                };

                var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };

                var response = await _httpClient.Value
                    .SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                return ParseResponse(body, sw.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return AIResponse.Fail($"llama-cpp request timed out after {timeoutMs}ms.", ProviderId, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                return AIResponse.Fail($"llama-cpp error: {ex.Message}", ProviderId, sw.ElapsedMilliseconds);
            }
        }

        private AIResponse ParseResponse(string body, long latencyMs)
        {
            try
            {
                using var doc  = JsonDocument.Parse(body);
                var root = doc.RootElement;

                // Error: { "error": { "code": 400, "message": "..." } }
                if (root.TryGetProperty("error", out var err))
                {
                    var msg = err.ValueKind == JsonValueKind.Object && err.TryGetProperty("message", out var em)
                        ? em.GetString()
                        : err.GetString();
                    return AIResponse.Fail(msg ?? "llama-cpp error", ProviderId, latencyMs);
                }

                // Success: { "content": "...", "tokens_predicted": N, "tokens_evaluated": N }
                var text = root.TryGetProperty("content", out var c)
                    ? c.GetString()?.Trim() ?? ""
                    : body.Trim();

                AITokenUsage? usage = null;
                if (root.TryGetProperty("tokens_evaluated", out var te) &&
                    root.TryGetProperty("tokens_predicted", out var tp))
                {
                    var inp  = te.GetInt32();
                    var out_ = tp.GetInt32();
                    usage = new AITokenUsage { PromptTokens = inp, ResponseTokens = out_, TotalTokens = inp + out_ };
                }

                return AIResponse.Ok(text, ProviderId, latencyMs, usage);
            }
            catch (Exception ex)
            {
                return AIResponse.Fail($"llama-cpp response parse error: {ex.Message}", ProviderId, latencyMs);
            }
        }
    }
}
