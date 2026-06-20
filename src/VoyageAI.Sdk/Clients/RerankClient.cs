using Microsoft.Extensions.Logging;
using VoyageAI.Internal;
using VoyageAI.Models;
using VoyageAI.Serialization;

namespace VoyageAI.Clients;

/// <summary>
/// Client for the Voyage AI rerank endpoint (<c>POST /rerank</c>). Validates the document
/// count client-side before dispatch.
/// </summary>
internal sealed class RerankClient : VoyageAIBaseClient, IRerankClient
{
    private readonly ILogger<RerankClient> _logger;

    /// <summary>Initializes a new <see cref="RerankClient"/>.</summary>
    public RerankClient(HttpClient httpClient, ILogger<RerankClient> logger)
        : base(httpClient, logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<RerankResponse> RerankAsync(
        RerankRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Client-side guard: fail fast with a clear message before hitting the network.
        Guard.ValidateDocuments(request.Documents);

        Log.RequestSent(_logger, "rerank", request.Model);

        return PostAsync(
            "rerank",
            request,
            VoyageAIJsonContext.Default.RerankRequest,
            VoyageAIJsonContext.Default.RerankResponse,
            cancellationToken);
    }
}
