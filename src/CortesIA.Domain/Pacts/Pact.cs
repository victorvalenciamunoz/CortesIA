using CortesIA.Domain.Actions;
using CortesIA.Domain.Courts;

namespace CortesIA.Domain.Pacts;

public enum PactKind
{
    NonAggression,
    RecurringTrade,
}

/// <summary>
/// An offer from one court to another. <paramref name="Terms"/> are only for recurring trade,
/// seen from the proposer's side (its partner must be <paramref name="To"/>).
/// </summary>
public sealed record PactProposal(PactKind Kind, CourtId From, CourtId To, int Turns, Trade? Terms = null);

/// <summary>An agreed pact, active from <paramref name="StartTurn"/> to <see cref="EndTurn"/> inclusive.</summary>
public sealed record Pact(PactKind Kind, CourtId Proposer, CourtId Partner, int StartTurn, int Turns, Trade? Terms = null)
{
    public const int FulfilReward = 1;
    public const int BreakPenalty = 3;

    public int EndTurn => StartTurn + Turns - 1;

    public IReadOnlyList<CourtId> Parties => [Proposer, Partner];

    public bool Binds(CourtId a, CourtId b) => (Proposer == a && Partner == b) || (Proposer == b && Partner == a);

    public CourtId Other(CourtId party) => party == Proposer ? Partner : Proposer;

    /// <summary>The action a party must play each turn of a recurring trade.</summary>
    public Trade RequiredTrade(CourtId party)
    {
        var terms = Terms ?? throw new InvalidOperationException("Only recurring trade pacts have terms.");
        return party == Proposer ? terms : terms.Mirrored(Proposer);
    }
}
