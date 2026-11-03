using System.Diagnostics;
using Microsoft.Extensions.AI;

namespace CortesIA.Ai.Models;

/// <summary>One call to a model, as the project's rules ask to record it.</summary>
/// <param name="Error">Null when the call went well.</param>
public sealed record CallRecord(
    DateTimeOffset At,
    string? Model,
    int? InputTokens,
    int? OutputTokens,
    double Seconds,
    string? FinishReason,
    string? Error)
{
    public bool Succeeded => Error is null;
}

/// <summary>
/// Middleware that times every call and hands a <see cref="CallRecord"/> to <paramref name="record"/>,
/// whether the call works or fails. The code that calls the model does not know it is there.
/// </summary>
public sealed class RecordingChatClient(IChatClient inner, Action<CallRecord> record, TimeProvider? clock = null)
    : DelegatingChatClient(inner)
{
    private readonly TimeProvider clock = clock ?? TimeProvider.System;

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var at = clock.GetUtcNow();
        var model = options?.ModelId ?? InnerClient.GetService<ChatClientMetadata>()?.DefaultModelId;
        var watch = Stopwatch.StartNew();
        try
        {
            var response = await base.GetResponseAsync(messages, options, cancellationToken);
            record(new CallRecord(at, response.ModelId ?? model, (int?)response.Usage?.InputTokenCount,
                (int?)response.Usage?.OutputTokenCount, watch.Elapsed.TotalSeconds, response.FinishReason?.Value, null));
            return response;
        }
        catch (Exception exception)
        {
            var error = exception is OperationCanceledException ? "Cancelada (tiempo agotado)." : $"{exception.GetType().Name}: {exception.Message}";
            record(new CallRecord(at, model, null, null, watch.Elapsed.TotalSeconds, null, error));
            throw;
        }
    }
}
