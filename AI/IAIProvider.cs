namespace SimpleSeleniumSupport.AI
{
    /// <summary>
    /// Abstraction over any large-language-model backend.
    /// <para>
    /// Implement this interface and register via <see cref="AIProviderRegistry.Register"/>
    /// to replace or extend the default Ollama provider.
    /// </para>
    /// </summary>
    public interface IAIProvider
    {
        /// <summary>
        /// Unique identifier shown in logs and telemetry (e.g. <c>"ollama"</c>, <c>"openai"</c>, <c>"anthropic"</c>).
        /// </summary>
        string ProviderId { get; }

        /// <summary>
        /// Send a single-turn text prompt and return a unified <see cref="AIResponse"/>.
        /// <para>
        /// Implementations are responsible for authentication, timeout, serialisation, and
        /// parsing. They must <b>never throw</b> past this interface — all errors are surfaced
        /// via <see cref="AIResponse.Success"/> and <see cref="AIResponse.ErrorMessage"/>.
        /// </para>
        /// </summary>
        Task<AIResponse> GenerateAsync(
            string prompt,
            AIRequestOptions options,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Synchronous wrapper. The default implementation calls
        /// <see cref="GenerateAsync"/> with <see cref="CancellationToken.None"/>.
        /// Override only if a sync-native path exists.
        /// </summary>
        AIResponse Generate(string prompt, AIRequestOptions options)
            => GenerateAsync(prompt, options, CancellationToken.None).GetAwaiter().GetResult();
    }
}
