using Microsoft.Extensions.Logging;
using VoyageAI.Internal;
using VoyageAI.Models;
using VoyageAI.Serialization;

namespace VoyageAI.Clients;

/// <summary>
/// Client for the Voyage AI text embeddings endpoint (<c>POST /embeddings</c>). Thin façade
/// over <see cref="VoyageAIBaseClient"/>; validates the batch client-side before dispatch.
/// </summary>
internal sealed class EmbeddingsClient : VoyageAIBaseClient, IEmbeddingsClient
{
    private readonly ILogger<EmbeddingsClient> _logger;

    /// <summary>Initializes a new <see cref="EmbeddingsClient"/>.</summary>
    public EmbeddingsClient(HttpClient httpClient, ILogger<EmbeddingsClient> logger)
        : base(httpClient, logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<EmbeddingResponse> EmbedAsync(
        EmbeddingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Client-side guard: fail fast with a clear message before hitting the network.
        Guard.ValidateBatchSize(request.Input);

        Log.RequestSent(_logger, "embeddings", request.Model);

        return PostAsync(
            "embeddings",
            request,
            VoyageAIJsonContext.Default.EmbeddingRequest,
            VoyageAIJsonContext.Default.EmbeddingResponse,
            cancellationToken);
    }
}
