using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using VoyageAI;
using VoyageAI.Configuration;
using VoyageAI.Models;

namespace VoyageAI.Samples.RerankRAG;

internal static class Program
{
    private static async Task Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddVoyageAI(opts =>
        {
            opts.BaseAddress = new Uri("https://ai.mongodb.com/v1/");
            opts.ClientSideRpmLimit = 3; // Enforce client-side rate limit of 3 RPM to test pacing
        });

        // Register a query-time IEmbeddingGenerator over the Voyage client. InputType.Query is
        // set because this generator is dedicated to embedding search queries (the MEAI
        // abstraction has no query/document notion, so VoyageAI exposes it as an option).
        // This is the keystone integration: the generator speaks the standard .NET AI
        // composition model, so caching/batching/OTel middleware could compose via the
        // returned builder (e.g. .UseOpenTelemetry()).
        builder.Services.AddVoyageEmbeddingGenerator(opts => opts.InputType = InputType.Query);

        var host = builder.Build();
        var services = host.Services;

        // Retrieve required clients.
        // - queryEmbedder: IEmbeddingGenerator used to embed queries at search time (registered
        //   above with InputType.Query).
        // - documentEmbedder: a second IEmbeddingGenerator instance over the same Voyage client,
        //   configured with InputType.Document for indexing corpus documents. The MEAI
        //   abstraction has no query/document notion, so a single generator can't serve both
        //   roles correctly — Voyage prepends a retrieval prompt per InputType, and a mismatch
        //   degrades retrieval quality. Two instances is the correct split.
        // - rerankClient: Voyage reranker for the second retrieval stage.
        var queryEmbedder = services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        var voyageClient = queryEmbedder.GetService(typeof(IEmbeddingsClient)) as IEmbeddingsClient
            ?? throw new InvalidOperationException("IEmbeddingsClient not available from the generator.");
        var documentEmbedder = new VoyageEmbeddingGenerator(voyageClient,
            new VoyageEmbeddingGeneratorOptions { InputType = InputType.Document });
        var rerankClient = services.GetRequiredService<IRerankClient>();

        // 2. Set up MongoDB Client
        var mongoUri = Environment.GetEnvironmentVariable("MONGODB_URI") ?? "mongodb://localhost:27017";
        var mongoClient = new MongoClient(mongoUri);
        var database = mongoClient.GetDatabase("rag_kb_db");
        var mongoCollection = database.GetCollection<KBDocument>("knowledge_base");

        // Seed/Insert data into MongoDB (embeds corpus documents with InputType.Document).
        await SeedMongoDBKnowledgeBaseAsync(mongoCollection, documentEmbedder);

        // 3. Build the search stage (transport-specific) and the Voyage rerank stage. These are
        //    the only two pieces the app supplies; the AgentFramework package composes them and
        //    hands the result to TextSearchProvider, replacing the ~80-line inline SearchAdapter.
        var searcher = new MongoVoyageSearcher<KBDocument>(
            mongoCollection, queryEmbedder, limit: 10, numberOfCandidates: 20);
        var reranker = new VoyageReranker(rerankClient, new VoyageRerankerOptions { TopK = 3 });

        // 4. Build the AIAgent, wiring RAG via UseVoyageRag (search + rerank in one call).
        //    KBDocument implements IVoyageSearchResultMetadata, so the default mapper populates
        //    SourceName/SourceLink without a custom ResultMapper.
        IChatClient chatClient = GetChatClientInstance(services); // Replace with your actual chat client instance

        AIAgent agent = chatClient.AsAIAgent(new ChatClientAgentOptions
        {
            ChatOptions = new()
            {
                Instructions = "You are a professional assistant. Answer questions using the provided context and cite source names/links."
            },
            // UseVoyageRag appends the composed TextSearchProvider and returns the same options.
        }.UseVoyageRag(searcher, reranker, options =>
        {
            options.RerankerOptions.TopK = 3; // Target context length: top 3 documents
            options.TextSearchOptions = new TextSearchProviderOptions
            {
                SearchTime = TextSearchProviderOptions.TextSearchBehavior.BeforeAIInvoke,
                RecentMessageMemoryLimit = 3
            };
        }));

        // Run multi-turn agent session
        var session = await agent.CreateSessionAsync();

        AgentResponse response = await agent.RunAsync("How should I store my hiking boots?", session);
        Console.WriteLine($"\n[Agent Response]:\n{response.Text}\n");
    }

    private static async Task SeedMongoDBKnowledgeBaseAsync(
        IMongoCollection<KBDocument> collection,
        IEmbeddingGenerator<string, Embedding<float>> documentEmbedder)
    {
        // 1. Ensure the collection is explicitly created before creating search indexes
        var database = collection.Database;
        var collectionName = collection.CollectionNamespace.CollectionName;
        var filter = new BsonDocument("name", collectionName);
        var collections = await database.ListCollectionsAsync(new ListCollectionsOptions { Filter = filter });
        if (!await collections.AnyAsync())
        {
            await database.CreateCollectionAsync(collectionName);
        }

        // 2. Define and create the Atlas Vector Search index if not already present.
        // The driver 3.9 CreateVectorSearchIndexModel<T> replaces the hand-built BsonDocument:
        // the field selector, similarity function, and dimension count are all strongly typed,
        // and the model derives from CreateSearchIndexModel so it passes straight to
        // SearchIndexes.CreateOneAsync. ListAsync(name) narrows the existence check to this
        // index instead of scanning the full index list element-by-element.
        const string VectorIndexName = "vector_index";
        var existing = await collection.SearchIndexes.ListAsync(VectorIndexName);
        if (!await existing.AnyAsync())
        {
            var indexModel = new CreateVectorSearchIndexModel<KBDocument>(
                field: x => x.Embedding,        // path: "embedding"
                name: VectorIndexName,
                similarity: VectorSimilarity.Cosine,
                dimensions: 1024);
            await collection.SearchIndexes.CreateOneAsync(indexModel);
            Console.WriteLine($"[Setup] Atlas Vector Search index '{VectorIndexName}' created.");
        }

        // Check if DB already populated
        var count = await collection.EstimatedDocumentCountAsync();
        if (count > 0)
        {
            return;
        }

        var sourceDocs = new[]
        {
            // 1. The True Target Document
            new { SourceName = "Boot Storage Manual", SourceLink = "https://contoso.com/boots-storage", Text = "Store leather hiking boots in a cool, dry, well-ventilated space. Avoid leaving them in direct sunlight or damp basements to prevent dry rot and mold growth." },

            // 2. High Lexical Distractor (Matches "store", "hiking boots", but wrong semantic intent: Retail/Clearance sale)
            new { SourceName = "Seattle Adventure Store clearing", SourceLink = "https://contoso.com/sale", Text = "The Seattle Adventure Store has a huge clearance sale on all hiking boots, camping gear, and tents next weekend. Best prices to buy boots!" },

            // 3. Category Distractor (Matches "store" and footwear, but wrong category: High heels/Sneakers)
            new { SourceName = "Fashion Footwear Storage Guide", SourceLink = "https://contoso.com/fashion-shoes", Text = "Store your high-heel dress shoes and sneakers on open shelves in your closet. Avoid storing them in plastic wraps to keep their shape." },

            // 4. Activity Distractor (Matches "hiking boots", but wrong action: Cleaning/Waxing, not storage)
            new { SourceName = "Footwear Cleaning Guide", SourceLink = "https://contoso.com/cleaning", Text = "Clean your hiking boots with a damp cloth after every hike, and apply waterproof wax to preserve leather texture." },

            // 5. Keyword Distractor (Matches "boots", "store", but wrong product: rain boots)
            new { SourceName = "Wet Weather Hardware Store", SourceLink = "https://contoso.com/hardware", Text = "Rain boots are great for wet conditions; you can buy them at any local hardware store." },

            // 6. Generic outdoor storage
            new { SourceName = "Tent Cleaning Instructions", SourceLink = "https://contoso.com/tent-care", Text = "Clean your tent with warm water and non-detergent soap. Dry completely before storage to avoid mildew." }
        };

        var documentsToInsert = new List<KBDocument>();

        /*
        // --- Option A: Batch Embedding Generation (commented out for rate-limiting testing)
        // documentEmbedder is configured with InputType.Document, so the whole batch is embedded
        // as corpus documents in one round-trip. GenerateAsync returns one Embedding<float> per
        // input, in submission order.
        var textsToEmbed = sourceDocs.Select(doc => doc.Text).ToArray();
        var batchEmbeddings = await documentEmbedder.GenerateAsync(textsToEmbed);

        for (int i = 0; i < sourceDocs.Length; i++)
        {
            documentsToInsert.Add(new KBDocument
            {
                SourceName = sourceDocs[i].SourceName,
                SourceLink = sourceDocs[i].SourceLink,
                Text = sourceDocs[i].Text,
                Embedding = batchEmbeddings[i].Vector
            });
        }
        */

        // --- Option B: Iterative Embedding Generation (active to test rate-limiting client-side guard)
        foreach (var doc in sourceDocs)
        {
            // Embed each corpus document via IEmbeddingGenerator. InputType.Document is baked
            // into the generator, so the call site no longer references InputType at all.
            var embedding = (await documentEmbedder.GenerateAsync([doc.Text]))[0];

            documentsToInsert.Add(new KBDocument
            {
                SourceName = doc.SourceName,
                SourceLink = doc.SourceLink,
                Text = doc.Text,
                Embedding = embedding.Vector
            });
        }

        await collection.InsertManyAsync(documentsToInsert);
        Console.WriteLine("[Setup] MongoDB Knowledge Base successfully seeded.");
    }

    private static IChatClient GetChatClientInstance(IServiceProvider services)
    {
        var customUrl = Environment.GetEnvironmentVariable("OPENAI_CUSTOM_URL") ?? "http://localhost:1234/v1/";
        var apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? "ignored";
        var model = Environment.GetEnvironmentVariable("OPENAI_MODEL") ?? "lfm2.5-8b-a1b";

        var options = new OpenAI.OpenAIClientOptions
        {
            Endpoint = new Uri(customUrl)
        };

        var chatClient = new OpenAI.Chat.ChatClient(
            model: model,
            credential: new System.ClientModel.ApiKeyCredential(apiKey),
            options: options
        );

        return chatClient.AsIChatClient();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// SEARCH STAGE
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// The transport-specific search stage of a Voyage RAG pipeline: embeds the query via the
/// standard <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> abstraction and runs a
/// MongoDB Atlas Vector Search. Implements <see cref="IVoyageRagSearcher{T}"/> so the
/// AgentFramework package can compose it with the Voyage reranker — this is the only
/// transport-aware code the app needs; reranking and result projection live in the package.
/// </summary>
/// <typeparam name="T">The document type, which must expose the embedding field and the
/// retrievable text. <typeparamref name="T"/> implementing <see cref="IVoyageSearchResultMetadata"/>
/// is what lets the package's default mapper populate citation metadata.</typeparam>
internal sealed class MongoVoyageSearcher<T> : IVoyageRagSearcher<T>
    where T : IVoyageMongoDocument
{
    private readonly IMongoCollection<T> _collection;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _queryEmbedder;
    private readonly int _limit;
    private readonly int _numberOfCandidates;

    /// <param name="collection">The MongoDB collection holding the embedded documents.</param>
    /// <param name="queryEmbedder">Query-time embedder (registered with
    /// <c>InputType.Query</c>). The MEAI abstraction has no query/document notion, so a
    /// query-dedicated generator is required for correct retrieval.</param>
    /// <param name="limit">How many final candidates to return (the candidate pool the
    /// reranker narrows down from).</param>
    /// <param name="numberOfCandidates">Atlas Vector Search candidate pool size (typically
    /// <c>2 × limit</c>).</param>
    public MongoVoyageSearcher(
        IMongoCollection<T> collection,
        IEmbeddingGenerator<string, Embedding<float>> queryEmbedder,
        int limit,
        int numberOfCandidates)
    {
        _collection = collection;
        _queryEmbedder = queryEmbedder;
        _limit = limit;
        _numberOfCandidates = numberOfCandidates;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<VoyageSearchResult<T>>> SearchAsync(
        string query, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"\n[Retrieval] User Query: \"{query}\"");

        // Step A: Generate the query embedding via the IEmbeddingGenerator (the standard
        // .NET AI abstraction). The generator was registered with InputType.Query, so the
        // Voyage request is built with the query retrieval hint without this call site
        // knowing about InputType at all.
        GeneratedEmbeddings<Embedding<float>> queryEmbeddings =
            await _queryEmbedder.GenerateAsync([query], cancellationToken: cancellationToken);
        ReadOnlyMemory<float> queryVector = queryEmbeddings[0].Vector;

        // Step B: Run MongoDB Atlas Vector Search using the fluent API.
        // Under the hood, QueryVector supports implicit conversion from ReadOnlyMemory<float>,
        // completely avoiding the need for .ToArray() allocations or double casting.
        List<T> candidates = await _collection.Aggregate()
            .VectorSearch(
                field: doc => doc.Embedding,      // The document field containing the vector
                queryVector: queryVector,         // Passes ReadOnlyMemory<float> natively
                limit: _limit,                    // Retrieve the candidate pool
                options: new VectorSearchOptions<T>
                {
                    IndexName = "vector_index",   // The name of the Atlas Vector Search index
                    NumberOfCandidates = _numberOfCandidates
                })
            .ToListAsync(cancellationToken);

        if (candidates.Count == 0)
        {
            Console.WriteLine("[Retrieval] MongoDB Vector Search returned 0 candidates.");
            return [];
        }

        Console.WriteLine($"[Retrieval] MongoDB returned {candidates.Count} candidate documents. Handing to rerank stage.");

        // Step C (rerank + projection) is handled by the AgentFramework package — the searcher
        // only returns the candidate pool, with each candidate's Text pulled from the document.
        return candidates.Select(doc => new VoyageSearchResult<T>
        {
            Record = doc,
            Text = doc.Text
        }).ToList();
    }
}

/// <summary>
/// Constraint on documents a <see cref="MongoVoyageSearcher{T}"/> can search: the vector
/// field for the fluent <c>VectorSearch</c> call and the text field the reranker scores.
/// </summary>
internal interface IVoyageMongoDocument
{
    /// <summary>The document's embedding vector (the Atlas Vector Search path).</summary>
    ReadOnlyMemory<float> Embedding { get; }

    /// <summary>The text the reranker scores and the agent receives as context.</summary>
    string Text { get; }
}

// ─────────────────────────────────────────────────────────────────────────────
// DATA MODELS
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Knowledge-base document. Implements <see cref="IVoyageSearchResultMetadata"/> so the
/// AgentFramework package's default result mapper populates
/// <c>SourceName</c>/<c>SourceLink</c> as citations, and
/// <see cref="IVoyageMongoDocument"/> so <see cref="MongoVoyageSearcher{T}"/> can read its
/// embedding and text fields generically.
/// </summary>
public sealed class KBDocument : IVoyageMongoDocument, IVoyageSearchResultMetadata
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("sourceName")]
    public string SourceName { get; set; } = string.Empty;

    [BsonElement("sourceLink")]
    public string SourceLink { get; set; } = string.Empty;

    [BsonElement("text")]
    public string Text { get; set; } = string.Empty;

    [BsonElement("embedding")]
    public ReadOnlyMemory<float> Embedding { get; set; } = ReadOnlyMemory<float>.Empty;
}
