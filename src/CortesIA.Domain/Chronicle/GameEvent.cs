using CortesIA.Domain.Actions;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Pacts;

namespace CortesIA.Domain.Chronicle;

/// <summary>Something that happened in a turn, as data. <see cref="Narrator"/> turns it into a sentence.</summary>
public abstract record GameEvent(int Turn)
{
    /// <summary>Most events are public; the details of a pact are only known to its parties.</summary>
    public virtual bool IsVisibleTo(CourtId court) => true;
}

public sealed record Cultivated(int Turn, CourtId Court, int Grain, int Gold) : GameEvent(Turn);

public sealed record Recruited(int Turn, CourtId Court, int Soldiers) : GameEvent(Turn);

public sealed record Fortified(int Turn, CourtId Court) : GameEvent(Turn);

/// <summary>The deal is seen from <paramref name="Court"/>'s side.</summary>
public sealed record Traded(int Turn, CourtId Court, Trade Deal) : GameEvent(Turn);

public sealed record TradeFailed(int Turn, CourtId Court, Trade Deal, string Reason) : GameEvent(Turn);

/// <summary><paramref name="Winner"/> is null only when a mutual attack ends in a tie.</summary>
public sealed record Fought(
    int Turn,
    CourtId Attacker,
    CourtId Defender,
    bool Mutual,
    bool DefenderFortified,
    int AttackerForce,
    int DefenderForce,
    CourtId? Winner,
    int StolenGrain,
    int StolenGold) : GameEvent(Turn);

public sealed record MessageSent(int Turn, CourtId From, CourtId To, string Text, PactProposal? Proposal) : GameEvent(Turn)
{
    public override bool IsVisibleTo(CourtId court) => court == From || court == To;
}

/// <summary><paramref name="Reason"/> is set when the proposal was invalid; null when the other court said no.</summary>
public sealed record ProposalRejected(int Turn, PactProposal Proposal, string? Reason) : GameEvent(Turn)
{
    public override bool IsVisibleTo(CourtId court) => court == Proposal.From || court == Proposal.To;
}

/// <summary>A player failed, took too long or broke a rule, and the match stepped in. Only that court is told.</summary>
public sealed record PlayerFaulted(int Turn, CourtId Court, string Reason) : GameEvent(Turn)
{
    public override bool IsVisibleTo(CourtId court) => court == Court;
}

public sealed record PactSigned(int Turn, Pact Pact) : GameEvent(Turn)
{
    public override bool IsVisibleTo(CourtId court) => Pact.Parties.Contains(court);
}

public sealed record PactFulfilled(int Turn, Pact Pact) : GameEvent(Turn)
{
    public override bool IsVisibleTo(CourtId court) => Pact.Parties.Contains(court);
}

/// <summary>Public: every court learns about it.</summary>
public sealed record PactBroken(int Turn, Pact Pact, CourtId Breaker) : GameEvent(Turn);

/// <summary>A party was eliminated: no reward and no penalty.</summary>
public sealed record PactCancelled(int Turn, Pact Pact) : GameEvent(Turn)
{
    public override bool IsVisibleTo(CourtId court) => Pact.Parties.Contains(court);
}

public sealed record SoldiersDeserted(int Turn, CourtId Court, int Soldiers) : GameEvent(Turn);

public sealed record CourtEliminated(int Turn, CourtId Court) : GameEvent(Turn);
