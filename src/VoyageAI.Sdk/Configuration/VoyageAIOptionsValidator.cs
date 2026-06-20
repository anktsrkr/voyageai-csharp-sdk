using Microsoft.Extensions.Options;

namespace VoyageAI.Configuration;

/// <summary>
/// Validates <see cref="VoyageAIOptions"/>. Runs explicit data-annotation-equivalent
/// checks and ensures that an API key is resolvable either from the configured value or
/// from the <c>VOYAGE_API_KEY</c> environment variable. When the configured key is blank,
/// the environment value is read here (never inside the HTTP handler) and assigned back so
/// downstream components see the resolved key.
/// </summary>
/// <remarks>
/// Validation is performed with hand-written checks rather than
/// <c>System.ComponentModel.DataAnnotations.Validator</c> because that API relies on
/// reflection and is not AOT/trim-safe — it raises <c>IL2026</c> under trimming. The
/// <see cref="VoyageAIOptions"/> attributes are kept for documentation/IDE tooling, but
/// the enforced bounds live here.
/// </remarks>
internal sealed class VoyageAIOptionsValidator : IValidateOptions<VoyageAIOptions>
{
    private const int MaxRetryAttemptsLower = 1;
    private const int MaxRetryAttemptsUpper = 10;
    private const int ClientSideRpmLimitLower = 1;
    private const int ClientSideRpmLimitUpper = 10_000;
    private const double CircuitBreakerFailureRatioLower = 0.0;
    private const double CircuitBreakerFailureRatioUpper = 1.0;

    /// <inheritdoc/>
    public ValidateOptionsResult Validate(string? name, VoyageAIOptions options)
    {
        // Resolve the API key from the environment when not set directly. This is the
        // single place the fallback happens; the HTTP handler reads only the resolved
        // value, never the environment itself.
        options.ApiKey = options.ResolveApiKey();

        var failures = new List<string>();

        // [Required] ApiKey
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            failures.Add(
                $"{nameof(VoyageAIOptions.ApiKey)} is required. Set it via the '{VoyageAIOptions.SectionName}' " +
                $"configuration section, the {nameof(VoyageAIOptions.ApiKey)} property, or the " +
                $"'{VoyageAIOptions.ApiKeyEnvironmentVariable}' environment variable.");
        }

        // [Range(0, 10)] MaxRetryAttempts
        if (options.MaxRetryAttempts < MaxRetryAttemptsLower ||
            options.MaxRetryAttempts > MaxRetryAttemptsUpper)
        {
            failures.Add(
                $"{nameof(VoyageAIOptions.MaxRetryAttempts)} must be between " +
                $"{MaxRetryAttemptsLower} and {MaxRetryAttemptsUpper}. " +
                $"Actual: {options.MaxRetryAttempts}.");
        }

        // [Range(1, 10000)] ClientSideRpmLimit
        if (options.ClientSideRpmLimit < ClientSideRpmLimitLower ||
            options.ClientSideRpmLimit > ClientSideRpmLimitUpper)
        {
            failures.Add(
                $"{nameof(VoyageAIOptions.ClientSideRpmLimit)} must be between " +
                $"{ClientSideRpmLimitLower} and {ClientSideRpmLimitUpper}. " +
                $"Actual: {options.ClientSideRpmLimit}.");
        }

        // [Range(0.0, 1.0)] CircuitBreakerFailureRatio
        if (options.CircuitBreakerFailureRatio < CircuitBreakerFailureRatioLower ||
            options.CircuitBreakerFailureRatio > CircuitBreakerFailureRatioUpper)
        {
            failures.Add(
                $"{nameof(VoyageAIOptions.CircuitBreakerFailureRatio)} must be between " +
                $"{CircuitBreakerFailureRatioLower} and {CircuitBreakerFailureRatioUpper}. " +
                $"Actual: {options.CircuitBreakerFailureRatio}.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
