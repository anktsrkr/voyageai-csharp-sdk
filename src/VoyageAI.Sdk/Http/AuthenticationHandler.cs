using VoyageAI.Configuration;

namespace VoyageAI.Http;

/// <summary>
/// Attaches the <c>Authorization: Bearer &lt;apiKey&gt;</c> header to every outgoing
/// request. The API key is read from <see cref="VoyageAIOptions.ApiKey"/>, which the
/// options validator has already resolved from config or the <c>VOYAGE_API_KEY</c>
/// environment variable.
/// </summary>
internal sealed class AuthenticationHandler : DelegatingHandler
{
    private readonly IOptionsMonitor<VoyageAIOptions> _options;

    /// <summary>Initializes a new <see cref="AuthenticationHandler"/>.</summary>
    public AuthenticationHandler(IOptionsMonitor<VoyageAIOptions> options)
    {
        _options = options;
    }

    /// <inheritdoc/>
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Resolve live (not a captured snapshot) so key rotation / options reload works.
        var apiKey = _options.CurrentValue.ResolveApiKey();
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return base.SendAsync(request, cancellationToken);
    }
}
