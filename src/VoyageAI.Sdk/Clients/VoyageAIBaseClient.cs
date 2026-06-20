using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using VoyageAI.Internal;
using VoyageAI.Models;
using VoyageAI.Serialization;

namespace VoyageAI.Clients;

/// <summary>
/// Shared HTTP plumbing for the three endpoint clients. Centralizes JSON POST via the
/// source-generated <see cref="VoyageAIJsonContext"/>, structured error mapping, and
/// request/response logging so each concrete client stays a thin façade.
/// </summary>
internal abstract class VoyageAIBaseClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    /// <summary>Initializes a new <see cref="VoyageAIBaseClient"/>.</summary>
    protected VoyageAIBaseClient(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// POSTs <paramref name="request"/> to <paramref name="endpoint"/>, deserializing the
    /// success body as <typeparamref name="TResponse"/>. Non-2xx responses are mapped to
    /// the typed <see cref="VoyageAIException"/> hierarchy.
    /// </summary>
    protected async Task<TResponse> PostAsync<TRequest, TResponse>(
        string endpoint,
        TRequest request,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TRequest> requestTypeInfo,
        System.Text.Json.Serialization.Metadata.JsonTypeInfo<TResponse> responseTypeInfo,
        CancellationToken cancellationToken)
        where TResponse : class
    {
        using var content = JsonContent.Create(request, requestTypeInfo);
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint);
        message.Content = content;

        var stopwatch = Stopwatch.StartNew();
        using var response = await _httpClient
            .SendAsync(message, cancellationToken)
            .ConfigureAwait(false);
        stopwatch.Stop();

        if (response.IsSuccessStatusCode)
        {
            Log.ResponseReceived(
                _logger, endpoint, (int)response.StatusCode, stopwatch.ElapsedMilliseconds);

            return await response.Content
                .ReadFromJsonAsync(responseTypeInfo, cancellationToken)
                .ConfigureAwait(false)
                ?? throw new VoyageAIException(
                    $"The API returned an empty response body for {endpoint}.");
        }

        // Non-success: map to the typed exception hierarchy after reading the error body.
        var (detail, retryAfter) = await ReadErrorAsync(response, cancellationToken).ConfigureAwait(false);
        Log.RequestFailed(_logger, (int)response.StatusCode, detail);

        throw response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => new VoyageAIAuthException(detail)
            {
                ApiDetail = detail,
            },
            HttpStatusCode.TooManyRequests => new VoyageAIRateLimitException(detail)
            {
                ApiDetail = detail,
                RetryAfter = retryAfter,
            },
            HttpStatusCode.BadRequest => new VoyageAIValidationException(detail)
            {
                ApiDetail = detail,
            },
            _ => new VoyageAIException($"HTTP {(int)response.StatusCode} from {endpoint}: {detail}")
            {
                StatusCode = response.StatusCode,
                ApiDetail = detail,
            },
        };
    }

    /// <summary>
    /// Reads the API error envelope (<c>{ "detail": "..." }</c>) and the
    /// <c>Retry-After</c> header (if any) from a non-success response. Returns an empty
    /// detail when the body cannot be parsed, so the caller always has a usable message.
    /// </summary>
    private static async Task<(string detail, TimeSpan? retryAfter)> ReadErrorAsync(
        HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var detail = $"HTTP {(int)response.StatusCode} {response.StatusCode}.";

        try
        {
            await using var stream = await response.Content
                .ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);

            var error = await JsonSerializer
                .DeserializeAsync(stream, VoyageAIJsonContext.Default.VoyageAIErrorResponse, cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(error?.Detail))
            {
                detail = error.Detail;
            }
        }
        catch (JsonException)
        {
            // Body wasn't the expected error envelope; keep the synthesized status detail.
        }

        var retryAfter = TryParseRetryAfter(response);

        return (detail, retryAfter);
    }

    /// <summary>
    /// Parses the <c>Retry-After</c> header as either a delta-seconds or an HTTP-date,
    /// returning <see langword="null"/> when absent or unparseable.
    /// </summary>
    private static TimeSpan? TryParseRetryAfter(HttpResponseMessage response)
    {
        if (response.Headers?.RetryAfter is not { } retryAfter)
        {
            return null;
        }

        if (retryAfter.Delta is { } delta && delta > TimeSpan.Zero)
        {
            return delta;
        }

        if (retryAfter.Date is { } date)
        {
            var span = date - DateTimeOffset.UtcNow;
            return span > TimeSpan.Zero ? span : TimeSpan.Zero;
        }

        return null;
    }
}
