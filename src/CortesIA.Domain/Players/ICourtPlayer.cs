using CortesIA.Domain.Actions;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;
using CortesIA.Domain.Pacts;

namespace CortesIA.Domain.Players;

/// <summary>A message to another court. The sender is whoever returns it; a proposal must be from the sender to <paramref name="To"/>.</summary>
public sealed record Message(CourtId To, string Text, PactProposal? Proposal = null);

/// <summary>
/// Whoever decides for a court: a hand-written rival, a person at the console, later a model or an MCP client.
/// It only ever sees its own <see cref="CourtView"/>. Asynchronous and cancellable because a decision may be slow;
/// the match falls back to a default if it fails, takes too long or returns something illegal.
/// </summary>
public interface ICourtPlayer
{
    Task<IReadOnlyList<Message>> WriteMessagesAsync(CourtView view, CancellationToken cancellationToken);

    Task<bool> AnswerProposalAsync(CourtView view, PactProposal proposal, CancellationToken cancellationToken);

    Task<GameAction> ChooseActionAsync(CourtView view, CancellationToken cancellationToken);
}
