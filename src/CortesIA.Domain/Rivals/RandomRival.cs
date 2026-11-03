using CortesIA.Domain.Actions;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;
using CortesIA.Domain.Pacts;
using CortesIA.Domain.Players;

namespace CortesIA.Domain.Rivals;

/// <summary>
/// Picks at random among the legal actions, proposes a random pact now and then and accepts half of them.
/// The floor any real strategy must beat.
/// </summary>
public sealed class RandomRival(int seed) : ICourtPlayer
{
    private const double ProposalChance = 0.25;
    private const int MaxAmount = 3;
    private const int MaxPactTurns = 3;

    private readonly Random random = new(seed);

    public Task<IReadOnlyList<Message>> WriteMessagesAsync(CourtView view, CancellationToken cancellationToken)
    {
        var others = view.Others.Where(c => !c.IsEliminated).ToList();
        if (others.Count == 0 || random.NextDouble() >= ProposalChance)
        {
            return Task.FromResult<IReadOnlyList<Message>>([]);
        }

        var to = others[random.Next(others.Count)].Id;
        var kind = random.Next(2) == 0 ? PactKind.NonAggression : PactKind.RecurringTrade;
        var turns = random.Next(1, Math.Min(MaxPactTurns, GameState.LastTurn - view.Turn + 1) + 1);
        var terms = kind == PactKind.RecurringTrade ? RandomTrade(to, random.Next(1, MaxAmount + 1), RandomResource()) : null;
        return Task.FromResult<IReadOnlyList<Message>>([new Message(to, "", new PactProposal(kind, view.Self.Id, to, turns, terms))]);
    }

    public Task<bool> AnswerProposalAsync(CourtView view, PactProposal proposal, CancellationToken cancellationToken) =>
        Task.FromResult(random.Next(2) == 0);

    public Task<GameAction> ChooseActionAsync(CourtView view, CancellationToken cancellationToken)
    {
        var legal = view.Legal;
        List<Func<GameAction>> options = [() => new Cultivate(), () => new Fortify()];
        if (legal.CanRecruit)
        {
            options.Add(() => new Recruit());
        }

        if (legal.AttackTargets.Count > 0)
        {
            options.Add(() => new Attack(legal.AttackTargets[random.Next(legal.AttackTargets.Count)]));
        }

        if (legal.TradePartners.Count > 0)
        {
            options.Add(() =>
            {
                var partner = legal.TradePartners[random.Next(legal.TradePartners.Count)];
                var give = legal.MaxGrainToGive == 0 ? Resource.Gold
                    : legal.MaxGoldToGive == 0 ? Resource.Grain
                    : RandomResource();
                var max = give == Resource.Grain ? legal.MaxGrainToGive : legal.MaxGoldToGive;
                return RandomTrade(partner, random.Next(1, Math.Min(max, MaxAmount) + 1), give);
            });
        }

        return Task.FromResult(options[random.Next(options.Count)]());
    }

    private Resource RandomResource() => random.Next(2) == 0 ? Resource.Grain : Resource.Gold;

    private Trade RandomTrade(CourtId partner, int giveAmount, Resource give) =>
        new(partner, give, giveAmount, give == Resource.Grain ? Resource.Gold : Resource.Grain, random.Next(1, MaxAmount + 1));
}
