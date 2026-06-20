using VoyageAI.Tests.Unit.Helpers;

namespace VoyageAI.Tests.Unit.Configuration;

/// <summary>
/// White-box tests for <see cref="VoyageAIOptions"/>: default values, public constants,
/// and the internal <see cref="VoyageAIOptions.ResolveApiKey"/> fallback to the
/// <c>VOYAGE_API_KEY</c> environment variable.
/// </summary>
public class VoyageAIOptionsTests
{
    [Fact]
    public void Defaults_AreAppliedOnConstruction()
    {
        var options = new VoyageAIOptions();

        options.BaseAddress.Should().Be(new Uri("https://api.voyageai.com/v1/"));
        options.RequestTimeout.Should().Be(TimeSpan.FromSeconds(100));
        options.MaxRetryAttempts.Should().Be(3);
        options.ClientSideRpmLimit.Should().Be(1_900);
        options.CircuitBreakerFailureRatio.Should().Be(0.5);
        options.CircuitBreakerDuration.Should().Be(TimeSpan.FromSeconds(30));
        options.ApiKey.Should().BeEmpty();
    }

    [Fact]
    public void Constants_HaveExpectedValues()
    {
        VoyageAIOptions.SectionName.Should().Be("VoyageAI");
        VoyageAIOptions.ApiKeyEnvironmentVariable.Should().Be("VOYAGE_API_KEY");
    }

    // --- ResolveApiKey is internal; env-mutating tests are serialized via the collection. ---

    [Collection("Environment")]
    public class ResolveApiKeyTests
    {
        private const string EnvVar = VoyageAIOptions.ApiKeyEnvironmentVariable;

        [Fact]
        public void ResolveApiKey_KeySet_ReturnsExplicitKey()
        {
            var original = Environment.GetEnvironmentVariable(EnvVar);
            try
            {
                Environment.SetEnvironmentVariable(EnvVar, "env-key");
                var options = new VoyageAIOptions { ApiKey = "explicit-key" };

                options.ResolveApiKey().Should().Be("explicit-key");
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvVar, original);
            }
        }

        [Fact]
        public void ResolveApiKey_KeyEmpty_WithEnv_ReturnsEnvValue()
        {
            var original = Environment.GetEnvironmentVariable(EnvVar);
            try
            {
                Environment.SetEnvironmentVariable(EnvVar, "env-resolved-key");
                var options = new VoyageAIOptions { ApiKey = "" };

                options.ResolveApiKey().Should().Be("env-resolved-key");
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvVar, original);
            }
        }

        [Fact]
        public void ResolveApiKey_BothEmpty_ReturnsEmptyString()
        {
            var original = Environment.GetEnvironmentVariable(EnvVar);
            try
            {
                Environment.SetEnvironmentVariable(EnvVar, null);
                var options = new VoyageAIOptions { ApiKey = "" };

                options.ResolveApiKey().Should().BeEmpty();
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvVar, original);
            }
        }

        [Theory]
        [InlineData("   ")]   // whitespace-only key falls back to env
        [InlineData("\t")]
        public void ResolveApiKey_WhitespaceKey_FallsBackToEnv(string key)
        {
            var original = Environment.GetEnvironmentVariable(EnvVar);
            try
            {
                Environment.SetEnvironmentVariable(EnvVar, "env-key");
                var options = new VoyageAIOptions { ApiKey = key };

                options.ResolveApiKey().Should().Be("env-key");
            }
            finally
            {
                Environment.SetEnvironmentVariable(EnvVar, original);
            }
        }
    }
}
