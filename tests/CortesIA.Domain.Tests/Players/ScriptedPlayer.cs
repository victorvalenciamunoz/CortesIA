using CortesIA.Domain.Actions;
using CortesIA.Domain.Game;
using CortesIA.Domain.Pacts;
using CortesIA.Domain.Players;

namespace CortesIA.Domain.Tests.Players;

/// <summary>A player whose every answer is set by the test. By default it says nothing, refuses and cultivates.</summary>
internal sealed class ScriptedPlayer : ICourtPlayer
{
    public Func<CourtView, IReadOnlyList<Message>> Messages { get; init; } = _ => [];

    public Func<CourtView, PactProposal, bool> Answer { get; init; } = (_, _) => false;

    public Func<CourtView, Task<GameAction>> Action { get; init; } = _ => Task.FromResult<GameAction>(new Cultivate());

    public List<CourtView> SeenViews { get; } = [];

    public int ProposalsReceived { get; private set; }

    public Task<IReadOnlyList<Message>> WriteMessagesAsync(CourtView view, CancellationToken cancellationToken) =>
        Task.FromResult(Messages(view));

    public Task<bool> AnswerProposalAsync(CourtView view, PactProposal proposal, CancellationToken cancellationToken)
    {
        ProposalsReceived++;
        return Task.FromResult(Answer(view, proposal));
    }

    public Task<GameAction> ChooseActionAsync(CourtView view, CancellationToken cancellationToken)
    {
        SeenViews.Add(view);
        return Action(view);
    }
}
