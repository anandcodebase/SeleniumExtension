using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace SimpleSeleniumSupport.AI.Providers
{
    /// <summary>
    /// <see cref="IAIProvider"/> implementation that calls a local (or remote) Ollama server.
    /// This is the default provider; it wraps the existing <see cref="OllamaClient"/> HTTP logic.
    /// </summary>
    public sealed class OllamaAIProvider : IAIProvider
    {
        private readonly string _baseUrl;
        private readonly string _defaultModel;
        private readonly int _defaultTimeoutMs;

        private static readonly HttpClient _httpClient = new();

        /// <inheritdoc/>
        public string ProviderId => "ollama";

        /// <summary>Creates an Ollama provider using <see cref="SimpleSeleniumSupportDefaults"/> values.</summary>
        public OllamaAIProvider()
        {
            _baseUrl        = SimpleSeleniumSupportDefaults.OllamaBaseUrl;
            _defaultModel   = SimpleSeleniumSupportDefaults.OllamaModel;
            _defaultTimeoutMs = SimpleSeleniumSupportDefaults.OllamaTimeoutMs;
        }

        /// <summary>Creates an Ollama provider with explicit configuration.</summary>
        public OllamaAIProvider(AIProviderConfig config)
        {
            _baseUrl        = config.BaseUrl ?? SimpleSeleniumSupportDefaults.OllamaBaseUrl;
            _defaultModel   = config.Model   ?? SimpleSeleniumSupportDefaults.OllamaModel;
            _defaultTimeoutMs = config.TimeoutMs;
        }

        /// <inheritdoc/>
        public async Task<AIResponse> GenerateAsync(
            string prompt,
            AIRequestOptions options,
            CancellationToken cancellationToken = default)
        {
            if (OllamaClient.MockMode)
                return AIResponse.Ok("//body", ProviderId, 0);

            var model      = options.Model ?? _defaultModel;
            var timeoutMs  = options.TimeoutMs ?? _defaultTimeoutMs;
            var sw         = Stopwatch.StartNew();

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            try
            {
                var payload = new { model, prompt, stream = false };
                var jsonPayload = JsonSerializer.Serialize(payload);

                var request = new HttpRequestMessage(HttpMethod.Post, _baseUrl)
                {
                    Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
                };
                OllamaClient.ConfigureRequest?.Invoke(request);

                var response = await _httpClient
                    .SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                var text = ParseOllamaResponse(body);

                var usage = ParseTokenUsage(body);
                return AIResponse.Ok(text, ProviderId, sw.ElapsedMilliseconds, usage);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return AIResponse.Fail($"Ollama request timed out after {timeoutMs}ms.", ProviderId, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                return AIResponse.Fail($"Ollama error: {ex.Message}", ProviderId, sw.ElapsedMilliseconds);
            }
        }

        private static string ParseOllamaResponse(string body)
        {
            try
            {
                using var doc  = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("response", out var r) && r.ValueKind == JsonValueKind.String)
                    return StripThinkingTags(r.GetString()!.Trim());

                if (root.TryGetProperty("outputs", out var outputs) &&
                    outputs.ValueKind == JsonValueKind.Array &&
                    outputs.GetArrayLength() > 0)
                {
                    var first = outputs[0];
                    if (first.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.String)
                        return StripThinkingTags(c.GetString()!.Trim());
                    if (first.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
                        return StripThinkingTags(t.GetString()!.Trim());
                }

                if (root.ValueKind == JsonValueKind.String)
                    return StripThinkingTags(root.GetString()!.Trim());
            }
            catch (JsonException) { /* fall through */ }
            return StripThinkingTags(body.Trim());
        }

        // Remove <think>...</think> blocks emitted by reasoning models (Qwen3, QwQ, DeepSeek-R1, etc.)
        // These thinking tokens appear before the actual structured response and break the block parser.
        private static string StripThinkingTags(string text) =>
            Regex.Replace(text, @"<think>[\s\S]*?</think>\s*", "", RegexOptions.IgnoreCase).Trim();

        private static AITokenUsage? ParseTokenUsage(string body)
        {
            try
            {
                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;
                if (root.TryGetProperty("prompt_eval_count", out var p) &&
                    root.TryGetProperty("eval_count", out var e))
                {
                    int prompt = p.GetInt32();
                    int eval   = e.GetInt32();
                    return new AITokenUsage { PromptTokens = prompt, ResponseTokens = eval, TotalTokens = prompt + eval };
                }
            }
            catch { /* optional */ }
            return null;
        }
    }
}
