using SimpleSeleniumSupport.AI.Providers;

namespace SimpleSeleniumSupport.AI
{
    /// <summary>
    /// Central registry for <see cref="IAIProvider"/> implementations.
    /// <para>
    /// Thread-safe; register once at suite startup before any AI calls.
    /// </para>
    /// <example>
    /// <code>
    /// // Use OpenAI:
    /// AIProviderRegistry.Use("openai", new AIProviderConfig
    /// {
    ///     ApiKey = Environment.GetEnvironmentVariable("OPENAI_KEY"),
    ///     Model  = "gpt-4o-mini"
    /// });
    ///
    /// // Or register a custom implementation:
    /// AIProviderRegistry.Register(new MyCustomAIProvider());
    /// </code>
    /// </example>
    /// </summary>
    public static class AIProviderRegistry
    {
        private static IAIProvider _current = new OllamaAIProvider();

        /// <summary>
        /// Replaces the active provider. Thread-safe via <see cref="Volatile"/>.
        /// </summary>
        public static void Register(IAIProvider provider)
        {
            ArgumentNullException.ThrowIfNull(provider);
            Volatile.Write(ref _current, provider);
        }

        /// <summary>The currently active <see cref="IAIProvider"/>.</summary>
        public static IAIProvider Current => Volatile.Read(ref _current);

        /// <summary>
        /// Register a built-in provider by its string ID.
        /// </summary>
        /// <param name="providerId">
        /// One of: <c>"ollama"</c>, <c>"openai"</c>, <c>"azure-openai"</c>,
        /// <c>"anthropic"</c>, <c>"gemini"</c>.
        /// </param>
        /// <param name="config">Provider-specific configuration (API key, base URL, model, etc.).</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="providerId"/> is not recognised.</exception>
        public static void Use(string providerId, AIProviderConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            Register(providerId.ToLowerInvariant() switch
            {
                "ollama"       => new OllamaAIProvider(config),
                "openai"       => new OpenAIProvider(config),
                "azure-openai" => new AzureOpenAIProvider(config),
                "anthropic"    => new AnthropicProvider(config),
                "gemini"       => new GeminiProvider(config),
                _              => throw new ArgumentException(
                    $"Unknown AI provider ID '{providerId}'. Valid values: ollama, openai, azure-openai, anthropic, gemini.",
                    nameof(providerId))
            });
        }
    }

    /// <summary>
    /// Generic configuration bag shared by all built-in <see cref="IAIProvider"/> implementations.
    /// </summary>
    public sealed class AIProviderConfig
    {
        /// <summary>API key for authenticating with the provider.</summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Base URL override.
        /// <list type="bullet">
        ///   <item><description>Ollama: defaults to <see cref="SimpleSeleniumSupportDefaults.OllamaBaseUrl"/></description></item>
        ///   <item><description>OpenAI: defaults to <c>https://api.openai.com/v1/chat/completions</c></description></item>
        ///   <item><description>Azure OpenAI: full endpoint URL including deployment (required)</description></item>
        ///   <item><description>Anthropic: defaults to <c>https://api.anthropic.com/v1/messages</c></description></item>
        ///   <item><description>Gemini: defaults to the generativelanguage.googleapis.com endpoint</description></item>
        /// </list>
        /// </summary>
        public string? BaseUrl { get; set; }

        /// <summary>Default model name used when <see cref="AIRequestOptions.Model"/> is not set.</summary>
        public string? Model { get; set; }

        /// <summary>HTTP request timeout in milliseconds. Defaults to <see cref="SimpleSeleniumSupportDefaults.OllamaTimeoutMs"/>.</summary>
        public int TimeoutMs { get; set; } = SimpleSeleniumSupportDefaults.OllamaTimeoutMs;

        /// <summary>Azure OpenAI API version (e.g. <c>"2024-02-01"</c>). Only used by <c>azure-openai</c>.</summary>
        public string? ApiVersion { get; set; }

        /// <summary>Azure OpenAI deployment name. Only used by <c>azure-openai</c>.</summary>
        public string? DeploymentId { get; set; }
    }
}
