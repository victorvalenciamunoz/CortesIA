using CortesIA.Domain.Actions;
using CortesIA.Domain.Chronicle;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;
using CortesIA.Domain.Pacts;
using CortesIA.Domain.Resolution;

namespace CortesIA.Domain.Players;

/// <summary>
/// Plays a game by asking each court's player, turn by turn. The players propose; the domain disposes:
/// anything that fails, takes too long or breaks a rule is recorded as a <see cref="PlayerFaulted"/>
/// and replaced by a safe default, so the game always goes on.
/// </summary>
public sealed class Match(
    IReadOnlyDictionary<CourtId, ICourtPlayer> players,
    IDice dice,
    TimeSpan? decisionTimeout = null,
    GameState? start = null)
{
    public const int MaxMessagesPerRecipient = 2;

    /// <summary>Always legal for a living court.</summary>
    public static readonly GameAction DefaultAction = new Cultivate();

    public GameState State { get; private set; } = start ?? GameState.Start();

    /// <summary>Plays one turn and returns everything that happened in it (for every court).</summary>
    public async Task<IReadOnlyList<GameEvent>> PlayTurnAsync(CancellationToken cancellationToken = default)
    {
        if (State.IsOver)
        {
            throw new InvalidOperationException("The game is over.");
        }

        var before = State.Chronicle.Count;
        await DiplomacyAsync(cancellationToken);
        var actions = await ChooseActionsAsync(cancellationToken);
        State = TurnResolver.Resolve(State, actions, dice).State;
        return State.Chronicle.Skip(before).ToList();
    }

    public async Task<Standings> PlayToEndAsync(CancellationToken cancellationToken = default)
    {
        while (!State.IsOver)
        {
            await PlayTurnAsync(cancellationToken);
        }

        return Standings.Of(State);
    }

    private List<CourtId> Alive() => State.Courts.Values.Where(c => !c.IsEliminated).Select(c => c.Id).Order().ToList();

    // Courts speak one after another in a fixed order; a proposal is answered on the spot and the answer is not a message.
    private async Task DiplomacyAsync(CancellationToken cancellationToken)
    {
        foreach (var from in Alive())
        {
            var (messages, fault) = await AskAsync(from, (player, token) => player.WriteMessagesAsync(CourtView.For(State, from), token), cancellationToken);
            if (fault is not null)
            {
                Fault(from, fault);
                continue;
            }

            var sent = new Dictionary<CourtId, int>();
            foreach (var message in messages!)
            {
                if (message is null || !Enum.IsDefined(message.To) || message.To == from || State.Court(message.To).IsEliminated)
                {
                    Fault(from, "Mensaje a una corte que no puede recibirlo.");
                    continue;
                }

                var count = sent.GetValueOrDefault(message.To);
                if (count >= MaxMessagesPerRecipient)
                {
                    Fault(from, $"Solo se pueden enviar {MaxMessagesPerRecipient} mensajes a {message.To.Spanish()} por turno.");
                    continue;
                }

                sent[message.To] = count + 1;
                Record(new MessageSent(State.Turn, from, message.To, message.Text ?? "", message.Proposal));
                if (message.Proposal is { } proposal)
                {
                    await HandleProposalAsync(from, message.To, proposal, cancellationToken);
                }
            }
        }
    }

    private async Task HandleProposalAsync(CourtId from, CourtId to, PactProposal proposal, CancellationToken cancellationToken)
    {
        if (proposal.From != from || proposal.To != to)
        {
            Fault(from, "Una propuesta debe ir de quien envía el mensaje a quien lo recibe.");
            return;
        }

        if (PactRules.Validate(State, proposal) is { IsValid: false } invalid)
        {
            Record(new ProposalRejected(State.Turn, proposal, invalid.Reason));
            return;
        }

        var (accepted, fault) = await AskAsync(to, (player, token) => player.AnswerProposalAsync(CourtView.For(State, to), proposal, token), cancellationToken);
        if (fault is not null)
        {
            Fault(to, $"{fault} La propuesta se da por rechazada.");
        }

        if (accepted)
        {
            State = PactRules.Sign(State, proposal);
        }
        else
        {
            Record(new ProposalRejected(State.Turn, proposal, null));
        }
    }

    // Actions are secret and simultaneous: every court decides from the same state, all at once.
    private async Task<Dictionary<CourtId, GameAction>> ChooseActionsAsync(CancellationToken cancellationToken)
    {
        var courts = Alive();
        var views = courts.ToDictionary(court => court, court => CourtView.For(State, court));
        var answers = await Task.WhenAll(courts.Select(court =>
            AskAsync(court, (player, token) => player.ChooseActionAsync(views[court], token), cancellationToken)));

        var actions = new Dictionary<CourtId, GameAction>();
        foreach (var (court, (action, fault)) in courts.Zip(answers))
        {
            var reason = fault ?? (ActionValidator.Validate(State, court, action!) is { IsValid: false } invalid
                ? $"Jugada ilegal: {invalid.Reason}"
                : null);
            if (reason is not null)
            {
                Fault(court, $"{reason} Se juega Cultivar.");
            }

            actions[court] = reason is null ? action! : DefaultAction;
        }

        return actions;
    }

    /// <summary>Returns the answer, or a fault if the player threw, timed out or returned nothing.</summary>
    private async Task<(T? Value, string? Fault)> AskAsync<T>(
        CourtId court, Func<ICourtPlayer, CancellationToken, Task<T>> ask, CancellationToken cancellationToken)
    {
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (decisionTimeout is { } timeout)
        {
            limit.CancelAfter(timeout);
        }

        try
        {
            // WaitAsync also cuts off a player that ignores the token.
            var value = await ask(players[court], limit.Token).WaitAsync(limit.Token);
            return value is null ? (default, "No devolvió ninguna respuesta.") : (value, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return (default, "Tardó demasiado en decidir.");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return (default, $"Falló al decidir: {exception.Message}");
        }
    }

    private void Fault(CourtId court, string reason) => Record(new PlayerFaulted(State.Turn, court, reason));

    private void Record(GameEvent gameEvent) => State = State with { Chronicle = State.Chronicle.Add(gameEvent) };
}
