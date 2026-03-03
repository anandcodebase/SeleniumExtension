namespace SimpleSeleniumSupport.AI
{
    /// <summary>
    /// Unified response returned by any <see cref="IAIProvider"/>.
    /// </summary>
    public sealed class AIResponse
    {
        /// <summary>The raw text returned by the model.</summary>
        public string Text { get; init; } = "";

        /// <summary>
        /// <see langword="true"/> when the provider returned a non-empty response without an error.
        /// <see langword="false"/> when the call failed or returned empty content — check <see cref="ErrorMessage"/>.
        /// </summary>
        public bool Success { get; init; }

        /// <summary>Non-null when <see cref="Success"/> is <see langword="false"/>.</summary>
        public string? ErrorMessage { get; init; }

        /// <summary>Provider ID that produced this response (e.g. <c>"ollama"</c>, <c>"openai"</c>).</summary>
        public string ProviderId { get; init; } = "";

        /// <summary>Wall-clock milliseconds the provider call took.</summary>
        public long LatencyMs { get; init; }

        /// <summary>
        /// Token usage if reported by the backend.
        /// May be <see langword="null"/> for providers that do not expose usage in their response.
        /// </summary>
        public AITokenUsage? TokenUsage { get; init; }

        // ── Factory helpers ──────────────────────────────────────────────────────

        /// <summary>Creates a successful response.</summary>
        public static AIResponse Ok(string text, string providerId, long latencyMs, AITokenUsage? usage = null)
            => new() { Text = text, Success = true, ProviderId = providerId, LatencyMs = latencyMs, TokenUsage = usage };

        /// <summary>Creates a failed response.</summary>
        public static AIResponse Fail(string error, string providerId, long latencyMs)
            => new() { Success = false, ErrorMessage = error, ProviderId = providerId, LatencyMs = latencyMs };
    }

    /// <summary>Token usage reported by an <see cref="IAIProvider"/>.</summary>
    public sealed class AITokenUsage
    {
        /// <summary>Tokens in the prompt/input.</summary>
        public int PromptTokens { get; init; }
        /// <summary>Tokens in the response/output.</summary>
        public int ResponseTokens { get; init; }
        /// <summary>Total tokens consumed.</summary>
        public int TotalTokens { get; init; }
    }

    /// <summary>Per-call options passed to <see cref="IAIProvider.GenerateAsync"/>.</summary>
    public sealed class AIRequestOptions
    {
        /// <summary>Model name override. <see langword="null"/> uses the provider's default.</summary>
        public string? Model { get; set; }

        /// <summary>Maximum tokens to generate. <see langword="null"/> uses the provider's default.</summary>
        public int? MaxTokens { get; set; }

        /// <summary>Temperature (0.0–2.0). <see langword="null"/> uses the provider's default.</summary>
        public double? Temperature { get; set; }

        /// <summary>Per-call timeout override in milliseconds. <see langword="null"/> uses the provider's default.</summary>
        public int? TimeoutMs { get; set; }

        /// <summary>Returns a default options instance (all nulls — provider uses its configured defaults).</summary>
        public static AIRequestOptions Default => new();
    }
}
