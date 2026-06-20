using Microsoft.Extensions.Logging;
using VoyageAI.Internal;
using VoyageAI.Models;
using VoyageAI.Serialization;

namespace VoyageAI.Clients;

/// <summary>
/// Client for the Voyage AI multimodal embeddings endpoint
/// (<c>POST /multimodalembeddings</c>). Validates the input count client-side before
/// dispatch.
/// </summary>
internal sealed class MultimodalEmbeddingsClient : VoyageAIBaseClient, IMultimodalEmbeddingsClient
{
    private readonly ILogger<MultimodalEmbeddingsClient> _logger;

    /// <summary>Initializes a new <see cref="MultimodalEmbeddingsClient"/>.</summary>
    public MultimodalEmbeddingsClient(HttpClient httpClient, ILogger<MultimodalEmbeddingsClient> logger)
        : base(httpClient, logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<MultimodalEmbeddingResponse> EmbedAsync(
        MultimodalEmbeddingRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Client-side guard: fail fast with a clear message before hitting the network.
        Guard.ValidateMultimodalInputs(request.Inputs);

        Log.RequestSent(_logger, "multimodalembeddings", request.Model);

        return PostAsync(
            "multimodalembeddings",
            request,
            VoyageAIJsonContext.Default.MultimodalEmbeddingRequest,
            VoyageAIJsonContext.Default.MultimodalEmbeddingResponse,
            cancellationToken);
    }
}
