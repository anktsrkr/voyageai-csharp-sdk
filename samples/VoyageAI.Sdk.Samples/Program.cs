using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VoyageAI;
using VoyageAI.Configuration;
using VoyageAI.Models;

namespace VoyageAI.Samples;

/// <summary>
/// End-to-end sample for the Voyage AI SDK. Runs text embeddings, multimodal embeddings,
/// and reranking against the live API when <c>VOYAGE_API_KEY</c> (or the configured key) is
/// present; otherwise prints a friendly message and exits.
/// </summary>
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        var host = Host.CreateApplicationBuilder(args);

        host.Services.AddVoyageAI(opts =>
        {
            // Prefer the env var so the sample runs without an appsettings edit.
            var envKey = Environment.GetEnvironmentVariable(VoyageAIOptions.ApiKeyEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(envKey))
            {
                opts.ApiKey = envKey;
            }

            opts.MaxRetryAttempts = 3;
        });

        var app = host.Build();

        if (string.IsNullOrWhiteSpace(host.Configuration[VoyageAIOptions.ApiKeyEnvironmentVariable])
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(VoyageAIOptions.ApiKeyEnvironmentVariable)))
        {
            Console.WriteLine($"Set {VoyageAIOptions.ApiKeyEnvironmentVariable} to run this sample against the live API.");
            return 0;
        }

        var ct = CancellationToken.None;
        await RunEmbeddingsAsync(app.Services, ct);
        await RunQuantizedEmbeddingsAsync(app.Services, ct);
        await RunMultimodalAsync(app.Services, ct);
        await RunRerankAsync(app.Services, ct);

        return 0;
    }

    private static async Task RunEmbeddingsAsync(IServiceProvider services, CancellationToken ct)
    {
        Console.WriteLine("\n=== Text embeddings ===");
        var client = services.GetRequiredService<IEmbeddingsClient>();

        var request = new EmbeddingRequest
        {
            Model = VoyageAIModels.Voyage3,
            Input = new[] { "I like cats", "I also like dogs" },
            InputType = InputType.Document,
        };

        var response = await client.EmbedAsync(request, ct);

        Console.WriteLine($"Model: {response.Model}");
        Console.WriteLine($"Tokens: {response.Usage.TotalTokens}");
        foreach (var item in response.Data)
        {
            Console.WriteLine($"  #{item.Index}: {item.Embedding.Count} dims, " +
                              $"first 3 = [{item.Embedding[0]:0.000}, {item.Embedding[1]:0.000}, {item.Embedding[2]:0.000}]");
        }
    }

    private static async Task RunQuantizedEmbeddingsAsync(IServiceProvider services, CancellationToken ct)
    {
        Console.WriteLine("\n=== Quantized text embeddings (int8, base64) ===");
        var client = services.GetRequiredService<IEmbeddingsClient>();

        var request = new EmbeddingRequest
        {
            Model = VoyageAIModels.Voyage3Large,
            Input = new[] { "Enterprise search database record" },
            InputType = InputType.Document,
            OutputDtype = OutputDtype.Int8,
            EncodingFormat = EncodingFormat.Base64,
        };

        var response = await client.EmbedAsync(request, ct);

        Console.WriteLine($"Model: {response.Model}");
        foreach (var item in response.Data)
        {
            sbyte[] int8Vector = item.DecodeBase64<sbyte>();
            Console.WriteLine($"  #{item.Index}: Quantized (int8) dims = {int8Vector.Length}, " +
                              $"first 3 values = [{int8Vector[0]}, {int8Vector[1]}, {int8Vector[2]}]");
        }
    }

    private static async Task RunMultimodalAsync(IServiceProvider services, CancellationToken ct)
    {
        Console.WriteLine("\n=== Multimodal embeddings ===");
        var client = services.GetRequiredService<IMultimodalEmbeddingsClient>();

        var request = new MultimodalEmbeddingRequest
        {
            Model = VoyageAIModels.VoyageMultimodal3,
            Inputs =
            [
                MultimodalInput.From(new TextContentPart("A photo of a banana."))
            ],
        };

        try
        {
            var response = await client.EmbedAsync(request, ct);
            Console.WriteLine($"Model: {response.Model}");
            Console.WriteLine($"Text tokens: {response.Usage.TextTokens}, " +
                              $"image pixels: {response.Usage.ImagePixels}, " +
                              $"total: {response.Usage.TotalTokens}");
            Console.WriteLine($"  {response.Data.Count} embedding(s); " +
                              $"first vector has {response.Data[0].Embedding.Count} dims.");
        }
        catch (VoyageAIException ex)
        {
            Console.WriteLine($"  Skipped: {ex.Message}");
        }
    }

    private static async Task RunRerankAsync(IServiceProvider services, CancellationToken ct)
    {
        Console.WriteLine("\n=== Rerank ===");
        var client = services.GetRequiredService<IRerankClient>();

        var request = new RerankRequest
        {
            Model = VoyageAIModels.Rerank2Lite,
            Query = "When was the United Nations founded?",
            Documents =
            [
                "The United Nations was founded on 24 October 1945.",
                "Cats are popular domesticated mammals.",
                "The League of Nations was the predecessor to the UN."
            ],
            TopK = 2,
            ReturnDocuments = true,
        };

        var response = await client.RerankAsync(request, ct);

        Console.WriteLine($"Model: {response.Model}");
        Console.WriteLine($"Tokens: {response.Usage.TotalTokens}");
        foreach (var result in response.Data)
        {
            Console.WriteLine($"  #{result.Index} score={result.RelevanceScore:0.0000}: {result.Document}");
        }
    }
}
