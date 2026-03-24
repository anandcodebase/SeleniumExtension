using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace SimpleSeleniumSupport.AI.Providers
{
    /// <summary>
    /// Generic <see cref="IAIProvider"/> for any backend that implements the
    /// OpenAI Chat Completions API (<c>POST /v1/chat/completions</c>).
    /// <para>
    /// Works out of the box with — and is the backing implementation for — the following
    /// named providers in <see cref="AIProviderRegistry"/>:
    /// </para>
    /// <list type="bullet">
    ///   <item><description><b>Cloud:</b> Groq, Mistral AI, xAI Grok, Together AI, Perplexity AI, DeepSeek, Fireworks AI, Hugging Face Inference</description></item>
    ///   <item><description><b>Local:</b> LM Studio, Jan, LocalAI</description></item>
    ///   <item><description><b>Generic:</b> <c>"openai-compatible"</c> — point at any custom endpoint</description></item>
    /// </list>
    /// <example>
    /// <code>
    /// // Groq — fast cloud inference
    /// AIProviderRegistry.Use("groq", new AIProviderConfig { ApiKey = "gsk_...", Model = "llama3-70b-8192" });
    ///
    /// // LM Studio — no API key required, model is whatever is currently loaded
    /// AIProviderRegistry.Use("lm-studio", new AIProviderConfig { Model = "local-model" });
    ///
    /// // Any custom endpoint
    /// AIProviderRegistry.Use("openai-compatible", new AIProviderConfig
    /// {
    ///     BaseUrl = "http://my-server/v1/chat/completions",
    ///     Model   = "my-model",
    ///     ApiKey  = "optional"
    /// });
    /// </code>
    /// </example>
    /// </summary>
    public sealed class OpenAICompatibleProvider : IAIProvider
    {
        private readonly string  _baseUrl;
        private readonly string? _apiKey;
        private readonly string? _defaultModel;
        private readonly int     _defaultTimeoutMs;

        private static readonly Lazy<HttpClient> _httpClient = new(() => new HttpClient());

        /// <inheritdoc/>
        public string ProviderId { get; }

        /// <summary>
        /// Creates a provider targeting any OpenAI Chat Completions-compatible endpoint.
        /// </summary>
        /// <param name="config">Provider configuration (API key, base URL, model, timeout).</param>
        /// <param name="providerId">Identifier used in logs and <see cref="AIResponse.ProviderId"/> (e.g. <c>"groq"</c>).</param>
        /// <param name="defaultBaseUrl">
        /// Endpoint URL used when <see cref="AIProviderConfig.BaseUrl"/> is not set.
        /// </param>
        /// <param name="defaultModel">
        /// Default model name used when neither <see cref="AIProviderConfig.Model"/> nor
        /// <see cref="AIRequestOptions.Model"/> is set.  <see langword="null"/> omits the
        /// <c>model</c> field from the request body (some local servers use whatever is loaded).
        /// </param>
        public OpenAICompatibleProvider(
            AIProviderConfig config,
            string           providerId,
            string           defaultBaseUrl,
            string?          defaultModel = null)
        {
            ProviderId        = providerId;
            _baseUrl          = config.BaseUrl ?? defaultBaseUrl;
            _apiKey           = config.ApiKey;
            _defaultModel     = config.Model   ?? defaultModel;
            _defaultTimeoutMs = config.TimeoutMs;
        }

        /// <inheritdoc/>
        public async Task<AIResponse> GenerateAsync(
            string             prompt,
            AIRequestOptions   options,
            CancellationToken  cancellationToken = default)
        {
            var model     = options.Model ?? _defaultModel;
            var timeoutMs = options.TimeoutMs ?? _defaultTimeoutMs;
            var sw        = Stopwatch.StartNew();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            try
            {
                // Build JSON body — omit "model" key when null so local servers use their
                // currently-loaded model rather than rejecting an unknown model name.
                var body = new JsonObject
                {
                    ["messages"]    = new JsonArray(new JsonObject { ["role"] = "user", ["content"] = prompt }),
                    ["max_tokens"]  = options.MaxTokens  ?? 2048,
                    ["temperature"] = options.Temperature ?? 0.2
                };
                if (model != null) body["model"] = model;

                var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl)
                {
                    Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json")
                };
                if (!string.IsNullOrEmpty(_apiKey))
                    request.Headers.Add("Authorization", $"Bearer {_apiKey}");

                var response = await _httpClient.Value
                    .SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var responseBody = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                return ParseResponse(responseBody, sw.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return AIResponse.Fail($"{ProviderId} request timed out after {timeoutMs}ms.", ProviderId, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                return AIResponse.Fail($"{ProviderId} error: {ex.Message}", ProviderId, sw.ElapsedMilliseconds);
            }
        }

        private AIResponse ParseResponse(string body, long latencyMs)
        {
            try
            {
                using var doc  = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var err))
                    return AIResponse.Fail(err.GetProperty("message").GetString() ?? $"{ProviderId} error", ProviderId, latencyMs);

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
                        PromptTokens   = u.TryGetProperty("prompt_tokens",     out var pt) ? pt.GetInt32() : 0,
                        ResponseTokens = u.TryGetProperty("completion_tokens", out var ct) ? ct.GetInt32() : 0,
                        TotalTokens    = u.TryGetProperty("total_tokens",      out var tt) ? tt.GetInt32() : 0
                    };
                }

                return AIResponse.Ok(text, ProviderId, latencyMs, usage);
            }
            catch (Exception ex)
            {
                return AIResponse.Fail($"{ProviderId} response parse error: {ex.Message}", ProviderId, latencyMs);
            }
        }
    }
}
