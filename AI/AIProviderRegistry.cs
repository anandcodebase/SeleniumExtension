using SimpleSeleniumSupport.AI.Providers;

namespace SimpleSeleniumSupport.AI
{
    /// <summary>
    /// Central registry for <see cref="IAIProvider"/> implementations.
    /// <para>
    /// Thread-safe; register once at suite startup before any AI calls.
    /// The default provider is Ollama (local).
    /// </para>
    /// <para><b>Built-in provider IDs:</b></para>
    /// <list type="table">
    ///   <listheader><term>ID</term><description>Backend</description></listheader>
    ///   <item><term><c>"ollama"</c></term><description>Local/remote Ollama server (default)</description></item>
    ///   <item><term><c>"openai"</c></term><description>OpenAI GPT-4o, GPT-4o-mini, GPT-4-turbo …</description></item>
    ///   <item><term><c>"azure-openai"</c></term><description>Azure OpenAI Service deployment</description></item>
    ///   <item><term><c>"anthropic"</c></term><description>Anthropic Claude 3/4 models</description></item>
    ///   <item><term><c>"gemini"</c></term><description>Google Gemini 1.5 / 2.0</description></item>
    ///   <item><term><c>"groq"</c></term><description>Groq Cloud — fast LLaMA/Mixtral inference</description></item>
    ///   <item><term><c>"mistral"</c></term><description>Mistral AI — Mistral Large/Small/Nemo …</description></item>
    ///   <item><term><c>"xai"</c></term><description>xAI Grok models</description></item>
    ///   <item><term><c>"together"</c></term><description>Together AI open-model hosting</description></item>
    ///   <item><term><c>"perplexity"</c></term><description>Perplexity AI (sonar models)</description></item>
    ///   <item><term><c>"deepseek"</c></term><description>DeepSeek Chat / Coder models</description></item>
    ///   <item><term><c>"fireworks"</c></term><description>Fireworks AI model hosting</description></item>
    ///   <item><term><c>"huggingface"</c></term><description>Hugging Face Serverless Inference API</description></item>
    ///   <item><term><c>"cohere"</c></term><description>Cohere Command R / R+ models</description></item>
    ///   <item><term><c>"lm-studio"</c></term><description>LM Studio local server (localhost:1234)</description></item>
    ///   <item><term><c>"jan"</c></term><description>Jan local server (localhost:1337)</description></item>
    ///   <item><term><c>"local-ai"</c></term><description>LocalAI self-hosted server (localhost:8080)</description></item>
    ///   <item><term><c>"llama-cpp"</c></term><description>llama.cpp HTTP server /completion endpoint</description></item>
    ///   <item><term><c>"openai-compatible"</c></term><description>Any OpenAI Chat Completions-compatible endpoint (set <see cref="AIProviderConfig.BaseUrl"/>)</description></item>
    /// </list>
    /// <example>
    /// <code>
    /// // Cloud providers
    /// AIProviderRegistry.Use("openai",    new AIProviderConfig { ApiKey = "sk-...", Model = "gpt-4o" });
    /// AIProviderRegistry.Use("anthropic", new AIProviderConfig { ApiKey = "sk-ant-...", Model = "claude-sonnet-4-6" });
    /// AIProviderRegistry.Use("gemini",    new AIProviderConfig { ApiKey = "AIza...", Model = "gemini-2.0-flash" });
    /// AIProviderRegistry.Use("groq",      new AIProviderConfig { ApiKey = "gsk_...", Model = "llama3-70b-8192" });
    /// AIProviderRegistry.Use("mistral",   new AIProviderConfig { ApiKey = "...", Model = "mistral-large-latest" });
    /// AIProviderRegistry.Use("xai",       new AIProviderConfig { ApiKey = "xai-...", Model = "grok-beta" });
    /// AIProviderRegistry.Use("deepseek",  new AIProviderConfig { ApiKey = "...", Model = "deepseek-chat" });
    /// AIProviderRegistry.Use("cohere",    new AIProviderConfig { ApiKey = "...", Model = "command-r-plus" });
    ///
    /// // Local providers (no API key required)
    /// AIProviderRegistry.Use("ollama",    new AIProviderConfig { Model = "qwen3.5:4b" });
    /// AIProviderRegistry.Use("lm-studio", new AIProviderConfig { Model = "local-model" });
    /// AIProviderRegistry.Use("jan",       new AIProviderConfig { Model = "llama3" });
    /// AIProviderRegistry.Use("llama-cpp", new AIProviderConfig { BaseUrl = "http://localhost:8081/completion" });
    ///
    /// // Custom endpoint
    /// AIProviderRegistry.Use("openai-compatible", new AIProviderConfig
    /// {
    ///     BaseUrl = "http://my-server/v1/chat/completions",
    ///     Model   = "my-model"
    /// });
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
        /// One of the built-in provider IDs listed in the class summary.
        /// </param>
        /// <param name="config">Provider-specific configuration (API key, base URL, model, etc.).</param>
        /// <exception cref="ArgumentException">Thrown when <paramref name="providerId"/> is not recognised.</exception>
        public static void Use(string providerId, AIProviderConfig config)
        {
            ArgumentNullException.ThrowIfNull(config);
            Register(providerId.ToLowerInvariant() switch
            {
                // ── Original providers ──────────────────────────────────────────
                "ollama"       => new OllamaAIProvider(config),
                "openai"       => new OpenAIProvider(config),
                "azure-openai" => new AzureOpenAIProvider(config),
                "anthropic"    => new AnthropicProvider(config),
                "gemini"       => new GeminiProvider(config),

                // ── Cloud: OpenAI-compatible APIs ───────────────────────────────
                "groq" => new OpenAICompatibleProvider(config, "groq",
                    "https://api.groq.com/openai/v1/chat/completions",
                    "llama3-8b-8192"),

                "mistral" => new OpenAICompatibleProvider(config, "mistral",
                    "https://api.mistral.ai/v1/chat/completions",
                    "mistral-small-latest"),

                "xai" => new OpenAICompatibleProvider(config, "xai",
                    "https://api.x.ai/v1/chat/completions",
                    "grok-beta"),

                "together" => new OpenAICompatibleProvider(config, "together",
                    "https://api.together.xyz/v1/chat/completions",
                    "meta-llama/Llama-3-8b-chat-hf"),

                "perplexity" => new OpenAICompatibleProvider(config, "perplexity",
                    "https://api.perplexity.ai/chat/completions",
                    "llama-3.1-sonar-small-128k-online"),

                "deepseek" => new OpenAICompatibleProvider(config, "deepseek",
                    "https://api.deepseek.com/chat/completions",
                    "deepseek-chat"),

                "fireworks" => new OpenAICompatibleProvider(config, "fireworks",
                    "https://api.fireworks.ai/inference/v1/chat/completions",
                    "accounts/fireworks/models/llama-v3p1-8b-instruct"),

                "huggingface" or "hf" => new OpenAICompatibleProvider(config, "huggingface",
                    "https://api-inference.huggingface.co/v1/chat/completions",
                    null),

                // ── Cloud: Unique API format ────────────────────────────────────
                "cohere" => new CohereProvider(config),

                // ── Local: OpenAI-compatible ────────────────────────────────────
                "lm-studio" or "lmstudio" => new OpenAICompatibleProvider(config, "lm-studio",
                    "http://localhost:1234/v1/chat/completions",
                    null),

                "jan" => new OpenAICompatibleProvider(config, "jan",
                    "http://localhost:1337/v1/chat/completions",
                    null),

                "local-ai" or "localai" => new OpenAICompatibleProvider(config, "local-ai",
                    "http://localhost:8080/v1/chat/completions",
                    null),

                // ── Local: Unique API format ────────────────────────────────────
                "llama-cpp" or "llamacpp" => new LlamaCppProvider(config),

                // ── Generic fallback ────────────────────────────────────────────
                "openai-compatible" => new OpenAICompatibleProvider(config, "openai-compatible",
                    config.BaseUrl ?? throw new ArgumentException(
                        "openai-compatible provider requires AIProviderConfig.BaseUrl to be set.",
                        nameof(config)),
                    null),

                _ => throw new ArgumentException(
                    $"Unknown AI provider ID '{providerId}'. " +
                    "Valid values: ollama, openai, azure-openai, anthropic, gemini, groq, mistral, xai, " +
                    "together, perplexity, deepseek, fireworks, huggingface, cohere, " +
                    "lm-studio, jan, local-ai, llama-cpp, openai-compatible.",
                    nameof(providerId))
            });
        }
    }

    /// <summary>
    /// Generic configuration bag shared by all built-in <see cref="IAIProvider"/> implementations.
    /// </summary>
    public sealed class AIProviderConfig
    {
        /// <summary>API key for authenticating with the provider. Not required for local providers.</summary>
        public string? ApiKey { get; set; }

        /// <summary>
        /// Base URL override.
        /// <list type="bullet">
        ///   <item><description><b>Ollama:</b> defaults to <see cref="SimpleSeleniumSupportDefaults.OllamaBaseUrl"/></description></item>
        ///   <item><description><b>OpenAI:</b> defaults to <c>https://api.openai.com/v1/chat/completions</c></description></item>
        ///   <item><description><b>Azure OpenAI:</b> full endpoint URL including deployment (required)</description></item>
        ///   <item><description><b>Anthropic:</b> defaults to <c>https://api.anthropic.com/v1/messages</c></description></item>
        ///   <item><description><b>Gemini:</b> defaults to the generativelanguage.googleapis.com endpoint</description></item>
        ///   <item><description><b>Groq / Mistral / xAI etc.:</b> built-in defaults; override only to point at a proxy</description></item>
        ///   <item><description><b>LM Studio:</b> defaults to <c>http://localhost:1234/v1/chat/completions</c></description></item>
        ///   <item><description><b>Jan:</b> defaults to <c>http://localhost:1337/v1/chat/completions</c></description></item>
        ///   <item><description><b>LocalAI:</b> defaults to <c>http://localhost:8080/v1/chat/completions</c></description></item>
        ///   <item><description><b>llama-cpp:</b> defaults to <c>http://localhost:8081/completion</c></description></item>
        ///   <item><description><b>openai-compatible:</b> required — must be set to the full endpoint URL</description></item>
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
