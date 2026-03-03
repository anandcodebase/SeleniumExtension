using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace SimpleSeleniumSupport.AI.Providers
{
    /// <summary>
    /// <see cref="IAIProvider"/> implementation for Google Gemini (Generative Language API).
    /// Supports Gemini 1.5 Flash, Gemini 1.5 Pro, Gemini 2.0, and compatible models.
    /// </summary>
    public sealed class GeminiProvider : IAIProvider
    {
        private const string BaseUrlTemplate =
            "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent";
        private const string DefaultModel = "gemini-1.5-flash";

        private readonly string _defaultModel;
        private readonly int    _defaultTimeoutMs;
        private readonly string _apiKey;

        private static readonly Lazy<HttpClient> _httpClient = new(() => new HttpClient());

        /// <inheritdoc/>
        public string ProviderId => "gemini";

        /// <summary>Creates a Gemini provider with the given configuration.</summary>
        /// <exception cref="ArgumentException">Thrown when <see cref="AIProviderConfig.ApiKey"/> is missing.</exception>
        public GeminiProvider(AIProviderConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.ApiKey))
                throw new ArgumentException("Gemini requires an API key. Set AIProviderConfig.ApiKey.", nameof(config));

            _apiKey           = config.ApiKey;
            _defaultModel     = config.Model ?? DefaultModel;
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
                var url = string.Format(BaseUrlTemplate, model) + $"?key={_apiKey}";

                var payload = new
                {
                    contents = new[]
                    {
                        new
                        {
                            parts = new[] { new { text = prompt } }
                        }
                    },
                    generationConfig = new
                    {
                        maxOutputTokens = options.MaxTokens ?? 2048,
                        temperature     = (float)(options.Temperature ?? 0.2)
                    }
                };

                var request = new HttpRequestMessage(HttpMethod.Post, url)
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
                return AIResponse.Fail($"Gemini request timed out after {timeoutMs}ms.", ProviderId, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                return AIResponse.Fail($"Gemini error: {ex.Message}", ProviderId, sw.ElapsedMilliseconds);
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
                    return AIResponse.Fail(err.GetProperty("message").GetString() ?? "Gemini error", ProviderId, latencyMs);

                // candidates[0].content.parts[0].text
                var text = root
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString()?.Trim() ?? "";

                AITokenUsage? usage = null;
                if (root.TryGetProperty("usageMetadata", out var u))
                {
                    var inp  = u.TryGetProperty("promptTokenCount",     out var pt) ? pt.GetInt32() : 0;
                    var out_ = u.TryGetProperty("candidatesTokenCount", out var ct) ? ct.GetInt32() : 0;
                    var tot  = u.TryGetProperty("totalTokenCount",      out var tt) ? tt.GetInt32() : inp + out_;
                    usage = new AITokenUsage { PromptTokens = inp, ResponseTokens = out_, TotalTokens = tot };
                }

                return AIResponse.Ok(text, ProviderId, latencyMs, usage);
            }
            catch (Exception ex)
            {
                return AIResponse.Fail($"Gemini response parse error: {ex.Message}", ProviderId, latencyMs);
            }
        }
    }
}
