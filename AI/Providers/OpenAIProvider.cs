using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace SimpleSeleniumSupport.AI.Providers
{
    /// <summary>
    /// <see cref="IAIProvider"/> implementation for the OpenAI Chat Completions API.
    /// Supports GPT-4o, GPT-4o-mini, GPT-4-turbo, and any chat-completions-compatible model.
    /// </summary>
    public sealed class OpenAIProvider : IAIProvider
    {
        private const string DefaultBaseUrl = "https://api.openai.com/v1/chat/completions";
        private const string DefaultModel   = "gpt-4o-mini";

        private readonly string _baseUrl;
        private readonly string _defaultModel;
        private readonly int    _defaultTimeoutMs;
        private readonly string _apiKey;

        private static readonly Lazy<HttpClient> _httpClient = new(() => new HttpClient());

        /// <inheritdoc/>
        public string ProviderId => "openai";

        /// <summary>Creates an OpenAI provider with the given configuration.</summary>
        /// <exception cref="ArgumentException">Thrown when <see cref="AIProviderConfig.ApiKey"/> is missing.</exception>
        public OpenAIProvider(AIProviderConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.ApiKey))
                throw new ArgumentException("OpenAI requires an API key. Set AIProviderConfig.ApiKey.", nameof(config));

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
            var model     = options.Model      ?? _defaultModel;
            var timeoutMs = options.TimeoutMs  ?? _defaultTimeoutMs;
            var sw        = Stopwatch.StartNew();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            try
            {
                var payload = new
                {
                    model,
                    messages = new[] { new { role = "user", content = prompt } },
                    max_tokens  = options.MaxTokens ?? 2048,
                    temperature = options.Temperature ?? 0.2
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
                return AIResponse.Fail($"OpenAI request timed out after {timeoutMs}ms.", ProviderId, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                return AIResponse.Fail($"OpenAI error: {ex.Message}", ProviderId, sw.ElapsedMilliseconds);
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
                    return AIResponse.Fail(err.GetProperty("message").GetString() ?? "OpenAI error", ProviderId, latencyMs);

                var text = root
                    .GetProperty("choices")[0]
                    .GetProperty("message")
                    .GetProperty("content")
                    .GetString()?.Trim() ?? "";

                AITokenUsage? usage = null;
                if (root.TryGetProperty("usage", out var u))
                {
                    usage = new AITokenUsage
                    {
                        PromptTokens   = u.GetProperty("prompt_tokens").GetInt32(),
                        ResponseTokens = u.GetProperty("completion_tokens").GetInt32(),
                        TotalTokens    = u.GetProperty("total_tokens").GetInt32()
                    };
                }

                return AIResponse.Ok(text, ProviderId, latencyMs, usage);
            }
            catch (Exception ex)
            {
                return AIResponse.Fail($"OpenAI response parse error: {ex.Message}", ProviderId, latencyMs);
            }
        }
    }
}
