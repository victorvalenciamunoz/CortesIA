using CortesIA.Domain.Chronicle;
using CortesIA.Domain.Courts;
using Microsoft.Extensions.AI;

namespace CortesIA.Ai.Chronicle;

/// <summary>A turn told by the model, or why it could not be told. The game carries on either way.</summary>
public sealed record Narration(string? Text, string? Failure)
{
    public static Narration Told(string text) => new(text, null);

    public static Narration Failed(string reason) => new(null, reason);
}

/// <summary>
/// Asks a model to retell a turn. It gets the same fixed sentences the player sees (<see cref="Narrator"/>),
/// only those the reader is allowed to see, and has a time limit. It never throws: a failure is a <see cref="Narration"/> too.
/// </summary>
public sealed class Chronicler(IChatClient model, TimeSpan? timeout = null)
{
    // With qwen2.5-1.5b on this machine a turn takes 3 to 10 s; 10 s was cut short at least once.
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    private static readonly string SystemPrompt = Prompts.Load("chronicler.prompt.md");

    private readonly TimeSpan timeout = timeout ?? DefaultTimeout;

    public async Task<Narration> NarrateAsync(int turn, IEnumerable<GameEvent> events, CourtId reader, CancellationToken cancellationToken = default)
    {
        var facts = Facts(turn, events, reader);
        if (facts is null)
        {
            return Narration.Failed("No pasó nada que contar.");
        }

        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            ChatMessage[] messages = [new(ChatRole.System, SystemPrompt), new(ChatRole.User, facts)];
            var response = await model.GetResponseAsync(messages, new ChatOptions { MaxOutputTokens = 200 }, limit.Token).WaitAsync(limit.Token);
            var text = response.Text.Trim();
            return text.Length > 0 ? Narration.Told(text) : Narration.Failed("Respondió en blanco.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Narration.Failed($"Tardó más de {timeout.TotalSeconds:0} s.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Narration.Failed(exception.Message.Split('\n')[0].Trim());
        }
    }

    /// <summary>The user message: the turn's facts as a list, or null if the reader saw nothing.</summary>
    public static string? Facts(int turn, IEnumerable<GameEvent> events, CourtId reader)
    {
        var lines = events.Where(e => e.IsVisibleTo(reader)).Select(e => $"- {Narrator.Tell(e)}").ToList();
        return lines.Count == 0 ? null : $"Turno {turn}:\n{string.Join("\n", lines)}";
    }
}
