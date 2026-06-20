using Microsoft.Extensions.AI;

namespace VoyageAI.Tests.Unit.AgentFramework.Helpers;

/// <summary>
/// A minimal <see cref="IChatClient"/> stub that throws on actual chat calls. Used only to
/// construct a <see cref="ChatClientAgent"/> → <see cref="AIAgentBuilder"/> for wiring
/// tests; the test never runs the agent.
/// </summary>
public sealed class FakeChatClient : IChatClient
{
    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("FakeChatClient does not support chat.");

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException("FakeChatClient does not support streaming.");

    public object? GetService(Type serviceType, object? key = null) => null;

    public void Dispose() { /* no-op for tests */ }
}
