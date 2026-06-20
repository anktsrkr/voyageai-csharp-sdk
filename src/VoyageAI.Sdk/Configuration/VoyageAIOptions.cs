using System.ComponentModel.DataAnnotations;

namespace VoyageAI.Configuration;

/// <summary>
/// Configuration for the Voyage AI SDK. Bind from the <c>"VoyageAI"</c> configuration
/// section, set via the <see cref="O:Microsoft.Extensions.DependencyInjection.ServiceCollectionExtensions.AddVoyageAI"/> delegate,
/// or populate programmatically. Validated at startup via <c>ValidateOnStart</c>.
/// </summary>
public sealed class VoyageAIOptions
{
    /// <summary>The configuration section name (<c>"VoyageAI"</c>).</summary>
    public const string SectionName = "VoyageAI";

    /// <summary>
    /// Name of the environment variable read as a fallback when <see cref="ApiKey"/> is
    /// not configured explicitly: <c>VOYAGE_API_KEY</c>.
    /// </summary>
    public const string ApiKeyEnvironmentVariable = "VOYAGE_API_KEY";

    /// <summary>
    /// Bearer token used to authenticate API requests. If left empty at validation time,
    /// the SDK falls back to the <c>VOYAGE_API_KEY</c> environment variable. Must be
    /// non-empty after fallback.
    /// </summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Base address of the API. Defaults to <c>https://api.voyageai.com/v1/</c>.</summary>
    public Uri BaseAddress { get; set; } = new("https://api.voyageai.com/v1/");

    /// <summary>Per-request HTTP timeout. Default 100 seconds (matches the API's long-running embed calls).</summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(100);

    /// <summary>Maximum retry attempts on transient failures (429 / 5xx). Default 3. Minimum 1 (Polly v8 requires at least one retry attempt on the configured retry strategy).</summary>
    [Range(1, 10)]
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Client-side requests-per-minute guard. Prevents sending more than this many
    /// requests per minute regardless of server limits — a safety margin below the
    /// Voyage Basic 2000 RPM tier. Default 1900.
    /// </summary>
    [Range(1, 10_000)]
    public int ClientSideRpmLimit { get; set; } = 1_900;

    /// <summary>
    /// Circuit-breaker failure ratio (0.0–1.0). When the sampled failure rate exceeds
    /// this value the circuit opens and short-circuits further calls for
    /// <see cref="CircuitBreakerDuration"/>. Default 0.5.
    /// </summary>
    [Range(0.0, 1.0)]
    public double CircuitBreakerFailureRatio { get; set; } = 0.5;

    /// <summary>How long the circuit breaker stays open before probing again. Default 30 seconds.</summary>
    public TimeSpan CircuitBreakerDuration { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Resolves the effective API key: the explicitly configured value, or the
    /// <c>VOYAGE_API_KEY</c> environment variable when the configured value is blank.
    /// Used by the options validator and the auth handler.
    /// </summary>
    /// <returns>The non-empty API key, or <see cref="string.Empty"/> if neither source provides one.</returns>
    internal string ResolveApiKey() =>
        !string.IsNullOrWhiteSpace(ApiKey)
            ? ApiKey
            : Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable) ?? string.Empty;
}
