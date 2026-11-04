using CortesIA.Domain.Chronicle;
using CortesIA.Domain.Courts;
using Microsoft.Extensions.AI;

namespace CortesIA.Ai.Chronicle;

/// <summary>A turn told by the model, or why it could not be told. The game carries on either way.</summary>
/// <param name="Draft">What the model wrote when it was discarded by <see cref="NarrationCheck"/>, kept for the record.</param>
public sealed record Narration(string? Text, string? Failure, string? Draft = null)
{
    public static Narration Told(string text) => new(text, null);

    public static Narration Failed(string reason) => new(null, reason);

    public static Narration Discarded(string draft, string reason) => new(null, reason, draft);
}

/// <summary>One narration as it is recorded: what the model was given and what came of it.</summary>
public sealed record NarrationRecord(DateTimeOffset At, int Turn, string Facts, string? Text, string? Draft, string? Failure);

/// <summary>
/// Asks a model to retell a turn. It gets the same fixed sentences the player sees (<see cref="Narrator"/>),
/// only those the reader is allowed to see, and has a time limit. What comes back is checked before it is shown
/// (<see cref="NarrationCheck"/>). It never throws: a failure is a <see cref="Narration"/> too.
/// </summary>
/// <param name="record">Receives every narration the model was asked for, shown or not.</param>
/// <param name="systemPrompt">Only to compare prompts; the game uses <c>chronicler.prompt.md</c>.</param>
public sealed class Chronicler(
    IChatClient model,
    TimeSpan? timeout = null,
    Action<NarrationRecord>? record = null,
    string? systemPrompt = null,
    TimeProvider? clock = null)
{
    // With qwen2.5-1.5b on this machine a turn takes 3 to 20 s; 10 s was cut short at least once.
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(20);

    public static readonly string DefaultSystemPrompt = Prompts.Load("chronicler.prompt.md");

    private readonly TimeSpan timeout = timeout ?? DefaultTimeout;

    private readonly string systemPrompt = systemPrompt ?? DefaultSystemPrompt;

    private readonly TimeProvider clock = clock ?? TimeProvider.System;

    public async Task<Narration> NarrateAsync(int turn, IEnumerable<GameEvent> events, CourtId reader, CancellationToken cancellationToken = default)
    {
        var facts = Facts(turn, events, reader);
        if (facts is null)
        {
            return Narration.Failed("No pasó nada que contar.");
        }

        var at = clock.GetUtcNow();
        var narration = await AskAsync(facts, cancellationToken);
        record?.Invoke(new NarrationRecord(at, turn, facts, narration.Text, narration.Draft, narration.Failure));
        return narration;
    }

    /// <summary>The user message: the turn's facts as a list, or null if the reader saw nothing.</summary>
    public static string? Facts(int turn, IEnumerable<GameEvent> events, CourtId reader)
    {
        var lines = events.Where(e => e.IsVisibleTo(reader)).Select(e => $"- {Narrator.Tell(e)}").ToList();
        return lines.Count == 0 ? null : $"Turno {turn}:\n{string.Join("\n", lines)}";
    }

    private async Task<Narration> AskAsync(string facts, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        limit.CancelAfter(timeout);
        try
        {
            ChatMessage[] messages = [new(ChatRole.System, systemPrompt), new(ChatRole.User, facts)];
            var response = await model.GetResponseAsync(messages, new ChatOptions { MaxOutputTokens = 200 }, limit.Token).WaitAsync(limit.Token);
            var text = response.Text.Trim();
            if (text.Length == 0)
            {
                return Narration.Failed("Respondió en blanco.");
            }

            return NarrationCheck.Problem(text, facts) is { } problem ? Narration.Discarded(text, problem) : Narration.Told(text);
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
}
