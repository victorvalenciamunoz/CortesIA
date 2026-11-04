using Microsoft.Extensions.AI;

namespace CortesIA.Ai.Models;

/// <summary>Middleware that refuses calls once a run has used up its allowance, so a paid model cannot run away.</summary>
public sealed class CallBudgetChatClient(IChatClient inner, int maxCalls) : DelegatingChatClient(inner)
{
    private int calls;

    public int Calls => calls;

    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref calls) > maxCalls)
        {
            throw new InvalidOperationException($"Límite de {maxCalls} llamadas por partida alcanzado.");
        }

        return base.GetResponseAsync(messages, options, cancellationToken);
    }
}
