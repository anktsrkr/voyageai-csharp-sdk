using VoyageAI.Tests.Unit.Helpers;

namespace VoyageAI.Tests.Unit.Validation;

/// <summary>
/// Direct white-box tests on <see cref="VoyageAIOptionsValidator.Validate"/>. Covers every
/// rule (API key required + env fallback, MaxRetryAttempts range, ClientSideRpmLimit range,
/// CircuitBreakerFailureRatio range) and the aggregation of multiple failures into one
/// <see cref="ValidateOptionsResult.Fail"/>.
/// </summary>
public class VoyageAIOptionsValidatorTests
{
    private static VoyageAIOptions ValidOptions() => new() { ApiKey = "key" };

    private static ValidateOptionsResult Validate(VoyageAIOptions options) =>
        new VoyageAIOptionsValidator().Validate(name: null, options);

    // VoyageAIOptions is a class (not a record), so there is no `with` expression.
    // These helpers clone the valid baseline while overriding a single property.
    private static VoyageAIOptions With(VoyageAIOptions source, int maxRetryAttempts) =>
        new()
        {
            ApiKey = source.ApiKey,
            MaxRetryAttempts = maxRetryAttempts,
            ClientSideRpmLimit = source.ClientSideRpmLimit,
            CircuitBreakerFailureRatio = source.CircuitBreakerFailureRatio,
        };

    private static VoyageAIOptions With(VoyageAIOptions source, int rpm, bool _) =>
        new()
        {
            ApiKey = source.ApiKey,
            MaxRetryAttempts = source.MaxRetryAttempts,
            ClientSideRpmLimit = rpm,
            CircuitBreakerFailureRatio = source.CircuitBreakerFailureRatio,
        };

    private static VoyageAIOptions With(VoyageAIOptions source, double failureRatio) =>
        new()
        {
            ApiKey = source.ApiKey,
            MaxRetryAttempts = source.MaxRetryAttempts,
            ClientSideRpmLimit = source.ClientSideRpmLimit,
            CircuitBreakerFailureRatio = failureRatio,
        };

    [Fact]
    public void Validate_KeySet_ReturnsSuccess()
    {
        var result = Validate(new VoyageAIOptions { ApiKey = "explicit-key" });

        result.Succeeded.Should().BeTrue();
        result.Failed.Should().BeFalse();
    }

    [Fact]
    public void Validate_DefaultOptions_WithKey_SucceedsAndKeepsDefaults()
    {
        var options = new VoyageAIOptions { ApiKey = "key" };
        var result = Validate(options);

        result.Succeeded.Should().BeTrue();
        options.MaxRetryAttempts.Should().Be(3);
        options.ClientSideRpmLimit.Should().Be(1_900);
        options.CircuitBreakerFailureRatio.Should().Be(0.5);
    }

    [Theory]
    [InlineData(-1, false)]
    [InlineData(0, false)]   // Polly v8 requires MaxRetryAttempts >= 1; 0 is rejected.
    [InlineData(1, true)]
    [InlineData(10, true)]
    [InlineData(11, false)]
    public void Validate_MaxRetryAttempts_Range(int value, bool shouldSucceed)
    {
        var options = With(ValidOptions(), maxRetryAttempts: value);
        var result = Validate(options);

        (result.Succeeded == shouldSucceed).Should().BeTrue(
            because: $"MaxRetryAttempts={value} should {(shouldSucceed ? "pass" : "fail")}");
        if (!shouldSucceed)
        {
            result.Failures.Should().Contain(f => f.Contains("MaxRetryAttempts"));
        }
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(10_000, true)]
    [InlineData(10_001, false)]
    public void Validate_ClientSideRpmLimit_Range(int value, bool shouldSucceed)
    {
        var options = With(ValidOptions(), rpm: value, _: true);
        var result = Validate(options);

        (result.Succeeded == shouldSucceed).Should().BeTrue(
            because: $"ClientSideRpmLimit={value} should {(shouldSucceed ? "pass" : "fail")}");
        if (!shouldSucceed)
        {
            result.Failures.Should().Contain(f => f.Contains("ClientSideRpmLimit"));
        }
    }

    [Theory]
    [InlineData(-0.1, false)]
    [InlineData(0.0, true)]
    [InlineData(0.5, true)]
    [InlineData(1.0, true)]
    [InlineData(1.1, false)]
    public void Validate_CircuitBreakerFailureRatio_Range(double value, bool shouldSucceed)
    {
        var options = With(ValidOptions(), failureRatio: value);
        var result = Validate(options);

        (result.Succeeded == shouldSucceed).Should().BeTrue(
            because: $"CircuitBreakerFailureRatio={value} should {(shouldSucceed ? "pass" : "fail")}");
        if (!shouldSucceed)
        {
            result.Failures.Should().Contain(f => f.Contains("CircuitBreakerFailureRatio"));
        }
    }

    // NOTE: Validate_MultipleFailures_AreAggregated and the ApiKey-required message test
    // are env-sensitive (they assert an empty key yields the ApiKey failure, which only
    // holds when VOYAGE_API_KEY is unset). They live in EnvironmentFallbackTests below,
    // serialized so the env is controlled.

    // --- Env-fallback tests are serialized via the collection to avoid cross-test bleed. ---

    [Collection("Environment")]
    public class EnvironmentFallbackTests
    {
        private const string EnvVar = VoyageAIOptions.ApiKeyEnvironmentVariable; // VOYAGE_API_KEY

        [Fact]
        public void Validate_MultipleFailures_AreAggregated()
        {
            var original = Environment.GetEnvironmentVariable(EnvVar);
            try
            {
                // Ensure no env fallback so ApiKey="" triggers the ApiKey failure.
                Environment.SetEnvironmentVariable(EnvVar, null);

                var options = new VoyageAIOptions
                {
                    ApiKey = "",
                    MaxRetryAttempts = 11,
                    ClientSideRpmLimit = 10_001,
                    CircuitBreakerFailureRatio = 2.0,
                };

                var result = Validate(options);

                result.Failed.Should().BeTrue();
                result.Failures.Should().HaveCount(4);
                result.FailureMessage.Should().Contain("ApiKey");
                result.FailureMessage.Should().Contain("MaxRetryAttempts");
                result.FailureMessage.Should().Contain("ClientSideRpmLimit");
                result.FailureMessage.Should().Contain("CircuitBreakerFailureRatio");
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvVar, original);
            }
        }

        [Fact]
        public void Validate_FailureMessage_HasExactApiKeyText()
        {
            var original = Environment.GetEnvironmentVariable(EnvVar);
            try
            {
                Environment.SetEnvironmentVariable(EnvVar, null);

                var result = Validate(new VoyageAIOptions { ApiKey = "" });

                result.FailureMessage.Should().Contain("ApiKey is required");
                result.FailureMessage.Should().Contain("VOYAGE_API_KEY");
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvVar, original);
            }
        }

        [Fact]
        public void Validate_EmptyKey_WithEnvFallback_SucceedsAndPopulatesApiKey()
        {
            var original = Environment.GetEnvironmentVariable(EnvVar);
            try
            {
                Environment.SetEnvironmentVariable(EnvVar, "env-resolved-key");
                var options = new VoyageAIOptions { ApiKey = "" };

                var result = Validate(options);

                result.Succeeded.Should().BeTrue();
                options.ApiKey.Should().Be("env-resolved-key");
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvVar, original);
            }
        }

        [Fact]
        public void Validate_ExplicitKey_TakesPrecedenceOverEnv()
        {
            var original = Environment.GetEnvironmentVariable(EnvVar);
            try
            {
                Environment.SetEnvironmentVariable(EnvVar, "env-key");
                var options = new VoyageAIOptions { ApiKey = "explicit-key" };

                var result = Validate(options);

                result.Succeeded.Should().BeTrue();
                options.ApiKey.Should().Be("explicit-key");
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvVar, original);
            }
        }

        [Fact]
        public void Validate_EmptyKey_NoEnv_FailsWithRequiredMessage()
        {
            var original = Environment.GetEnvironmentVariable(EnvVar);
            try
            {
                Environment.SetEnvironmentVariable(EnvVar, null);
                var options = new VoyageAIOptions { ApiKey = "" };

                var result = Validate(options);

                result.Failed.Should().BeTrue();
                result.Failures.Should().ContainSingle();
                result.FailureMessage.Should().Contain("ApiKey is required");
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvVar, original);
            }
        }
    }
}

/// <summary>Marker collection that serializes all env-mutating tests.</summary>
[CollectionDefinition("Environment")]
public class EnvironmentCollection { }
