using CortesIA.Domain.Actions;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Game;
using CortesIA.Domain.Pacts;
using CortesIA.Domain.Players;
using CortesIA.Domain.Resolution;

namespace CortesIA.Domain.Rivals;

/// <summary>
/// A fixed, hand-written rule that grabs whatever it can safely take. Deterministic: no randomness.
/// It never proposes, and never breaks a pact (−3 costs more than the +2 of a victory).
/// </summary>
public sealed class GreedyRival : ICourtPlayer
{
    /// <summary>With two more soldiers it wins 8 times out of 9 against an unfortified court.</summary>
    public const int AttackMargin = 2;

    /// <summary>Not worth attacking a court with less grain + gold than this.</summary>
    public const int MinimumLoot = 2;

    public Task<IReadOnlyList<Message>> WriteMessagesAsync(CourtView view, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Message>>([]);

    /// <summary>Accepts non-aggression out of fear, and a trade only when it receives more than it gives.</summary>
    public Task<bool> AnswerProposalAsync(CourtView view, PactProposal proposal, CancellationToken cancellationToken)
    {
        var self = view.Self;
        var accept = proposal switch
        {
            { Kind: PactKind.NonAggression } => view.Others.Single(c => c.Id == proposal.From).Soldiers > self.Soldiers,
            { Kind: PactKind.RecurringTrade, Terms: { } terms } =>
                terms.GiveAmount > terms.AskAmount && self.Amount(terms.Ask) >= terms.AskAmount,
            _ => false,
        };
        return Task.FromResult(accept);
    }

    /// <summary>The first rule that applies: keep the agreed trade, feed the army, plunder, arm, farm.</summary>
    public Task<GameAction> ChooseActionAsync(CourtView view, CancellationToken cancellationToken) =>
        Task.FromResult(Choose(view));

    private static GameAction Choose(CourtView view)
    {
        var self = view.Self;

        foreach (var pact in view.Pacts.Where(p => p.Kind == PactKind.RecurringTrade))
        {
            var trade = pact.RequiredTrade(self.Id);
            if (self.Amount(trade.Give) >= trade.GiveAmount)
            {
                return trade;
            }
        }

        // Enough grain for this turn's upkeep and the next.
        if (self.Grain < 2 * TurnResolver.UpkeepCost(self.Soldiers))
        {
            return new Cultivate();
        }

        var victim = view.Others
            .Where(other => view.Legal.AttackTargets.Contains(other.Id))
            .Where(other => !view.Pacts.Any(p => p.Kind == PactKind.NonAggression && p.Binds(self.Id, other.Id)))
            .Where(other => self.Soldiers >= other.Soldiers + AttackMargin && other.Grain + other.Gold >= MinimumLoot)
            .OrderByDescending(other => other.Grain + other.Gold)
            .ThenBy(other => other.Id)
            .FirstOrDefault();
        if (victim is not null)
        {
            return new Attack(victim.Id);
        }

        var strongestRival = view.Others.Where(c => !c.IsEliminated).Select(c => c.Soldiers).DefaultIfEmpty(0).Max();
        if (view.Legal.CanRecruit && self.Soldiers <= strongestRival)
        {
            return new Recruit();
        }

        return new Cultivate();
    }
}
