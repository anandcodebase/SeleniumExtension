using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace SimpleSeleniumSupport.AI.Providers
{
    /// <summary>
    /// <see cref="IAIProvider"/> implementation for Azure OpenAI Service.
    /// <para>
    /// Requires <see cref="AIProviderConfig.BaseUrl"/> (the full Azure endpoint URL including deployment),
    /// <see cref="AIProviderConfig.ApiKey"/>, and optionally <see cref="AIProviderConfig.ApiVersion"/>.
    /// </para>
    /// <example>
    /// Endpoint format:
    /// <c>https://{resource}.openai.azure.com/openai/deployments/{deployment}/chat/completions</c>
    /// </example>
    /// </summary>
    public sealed class AzureOpenAIProvider : IAIProvider
    {
        private const string DefaultApiVersion = "2024-02-01";

        private readonly string _endpoint;
        private readonly string _apiKey;
        private readonly string _apiVersion;
        private readonly string _defaultModel;
        private readonly int    _defaultTimeoutMs;

        private static readonly Lazy<HttpClient> _httpClient = new(() => new HttpClient());

        /// <inheritdoc/>
        public string ProviderId => "azure-openai";

        /// <summary>Creates an Azure OpenAI provider with the given configuration.</summary>
        /// <exception cref="ArgumentException">
        /// Thrown when <see cref="AIProviderConfig.ApiKey"/> or <see cref="AIProviderConfig.BaseUrl"/> is missing.
        /// </exception>
        public AzureOpenAIProvider(AIProviderConfig config)
        {
            if (string.IsNullOrWhiteSpace(config.ApiKey))
                throw new ArgumentException("Azure OpenAI requires an API key. Set AIProviderConfig.ApiKey.", nameof(config));
            if (string.IsNullOrWhiteSpace(config.BaseUrl))
                throw new ArgumentException(
                    "Azure OpenAI requires BaseUrl = the full deployment endpoint URL. " +
                    "Format: https://{resource}.openai.azure.com/openai/deployments/{deployment}/chat/completions",
                    nameof(config));

            _apiKey           = config.ApiKey;
            _defaultModel     = config.Model      ?? config.DeploymentId ?? "gpt-4o";
            _apiVersion       = config.ApiVersion ?? DefaultApiVersion;
            _defaultTimeoutMs = config.TimeoutMs;

            // Append api-version query parameter if not already present
            var url = config.BaseUrl.TrimEnd('/');
            _endpoint = url.Contains("api-version") ? url : $"{url}?api-version={_apiVersion}";
        }

        /// <inheritdoc/>
        public async Task<AIResponse> GenerateAsync(
            string prompt,
            AIRequestOptions options,
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
                    messages    = new[] { new { role = "user", content = prompt } },
                    max_tokens  = options.MaxTokens ?? 2048,
                    temperature = options.Temperature ?? 0.2
                };

                var request = new HttpRequestMessage(HttpMethod.Post, _endpoint)
                {
                    Content = new StringContent(
                        JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
                };
                request.Headers.Add("api-key", _apiKey);

                var response = await _httpClient.Value
                    .SendAsync(request, HttpCompletionOption.ResponseContentRead, cts.Token)
                    .ConfigureAwait(false);
                response.EnsureSuccessStatusCode();

                var body = await response.Content.ReadAsStringAsync(cts.Token).ConfigureAwait(false);
                return ParseResponse(body, sw.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return AIResponse.Fail($"Azure OpenAI request timed out after {timeoutMs}ms.", ProviderId, sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                return AIResponse.Fail($"Azure OpenAI error: {ex.Message}", ProviderId, sw.ElapsedMilliseconds);
            }
        }

        private AIResponse ParseResponse(string body, long latencyMs)
        {
            try
            {
                using var doc  = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("error", out var err))
                    return AIResponse.Fail(err.GetProperty("message").GetString() ?? "Azure OpenAI error", ProviderId, latencyMs);

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
                return AIResponse.Fail($"Azure OpenAI response parse error: {ex.Message}", ProviderId, latencyMs);
            }
        }
    }
}
