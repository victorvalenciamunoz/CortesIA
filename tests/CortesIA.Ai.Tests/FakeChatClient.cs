using Microsoft.Extensions.AI;

namespace CortesIA.Ai.Tests;

/// <summary>A model whose answer is set by the test. It keeps the messages it was sent.</summary>
internal sealed class FakeChatClient(Func<CancellationToken, Task<ChatResponse>> answer) : IChatClient
{
    public List<ChatMessage[]> Calls { get; } = [];

    public static FakeChatClient Saying(string text, int inputTokens = 10, int outputTokens = 20) => new(_ => Task.FromResult(
        new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
        {
            ModelId = "modelo-falso",
            FinishReason = ChatFinishReason.Stop,
            Usage = new UsageDetails { InputTokenCount = inputTokens, OutputTokenCount = outputTokens },
        }));

    public static FakeChatClient Failing(Exception exception) => new(_ => Task.FromException<ChatResponse>(exception));

    /// <summary>Never answers and ignores cancellation, like a server that hangs.</summary>
    public static FakeChatClient Hanging() => new(_ => new TaskCompletionSource<ChatResponse>().Task);

    public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        Calls.Add(messages.ToArray());
        return answer(cancellationToken);
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException();

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
