using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace SimpleSeleniumSupport.AI.Providers
{
    /// <summary>
    /// <see cref="IAIProvider"/> implementation for the Anthropic Messages API.
    /// Supports Claude 3 Opus, Sonnet, Haiku, and Claude 3.5/4.x models.
    /// </summary>
    public sealed class AnthropicProvider : IAIProvider
    {
        private const string DefaultBaseUrl = "https://api.anthropic.com/v1/messages";
        private const string DefaultModel   = "claude-sonnet-4-6";
        private const string AnthropicVersion = "2023-06-01";

        private readonly string _baseUrl;
        private readonly string _defaultModel;
        private readonly int    _defaultTimeoutMs;
        private readonly string _apiKey;

        private static readonly Lazy<HttpClient> _httpClient = new(() => new HttpClient());

        /// <inheritdoc/>
        public string ProviderId => "anthropic";

        /// <summary>Creates an Anthropic provider with the given configuration.</summary>
        /// <exception cref="ArgumentException">Thrown when <see cref="AIProviderConfig.ApiKey"/> is missing.</exception>
        public AnthropicProvider(AIProviderConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.ApiKey))
                throw new ArgumentException("Anthropic requires an API key. Set AIProviderConfig.ApiKey.", nameof(config));

            _apiKey           = config.ApiKey;
            _baseUrl          = config.BaseUrl ?? DefaultBaseUrl;
            _defaultModel     = config.Model   ?? DefaultModel;
            _defaultTimeoutMs = config.TimeoutMs;
        }

        /// <inheritdoc/>
        public async Task<AIResponse> GenerateAsync(
            string prompt,
            AIRequestOptions options,
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
                    max_tokens  = options.MaxTokens ?? 2048,
                    messages    = new[] { new { role = "user", content = prompt } }
                };

                var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };
                request.Headers.Add("x-api-key", _apiKey);
                request.Headers.Add("anthropic-version", AnthropicVersion);

                var response = await _httpClient.Value
                    .SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                return ParseResponse(body, sw.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return AIResponse.Fail($"Anthropic request timed out after {timeoutMs}ms.", ProviderId, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                return AIResponse.Fail($"Anthropic error: {ex.Message}", ProviderId, sw.ElapsedMilliseconds);
            }
        }

        private AIResponse ParseResponse(string body, long latencyMs)
        {
            try
            {
                using var doc  = JsonDocument.Parse(body);
                var root = doc.RootElement;

                // Error response
                if (root.TryGetProperty("error", out var err))
                    return AIResponse.Fail(err.GetProperty("message").GetString() ?? "Anthropic error", ProviderId, latencyMs);

                // content[0].text
                var text = root
                    .GetProperty("content")[0]
                    .GetProperty("text")
                    .GetString()?.Trim() ?? "";

                AITokenUsage? usage = null;
                if (root.TryGetProperty("usage", out var u))
                {
                    var inp = u.TryGetProperty("input_tokens",  out var i) ? i.GetInt32() : 0;
                    var out_ = u.TryGetProperty("output_tokens", out var o) ? o.GetInt32() : 0;
                    usage = new AITokenUsage { PromptTokens = inp, ResponseTokens = out_, TotalTokens = inp + out_ };
                }

                return AIResponse.Ok(text, ProviderId, latencyMs, usage);
            }
            catch (Exception ex)
            {
                return AIResponse.Fail($"Anthropic response parse error: {ex.Message}", ProviderId, latencyMs);
            }
        }
    }
}
