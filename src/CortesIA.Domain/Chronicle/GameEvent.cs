using CortesIA.Domain.Actions;
using CortesIA.Domain.Courts;
using CortesIA.Domain.Pacts;

namespace CortesIA.Domain.Chronicle;

/// <summary>Something that happened in a turn, as data. Text is produced elsewhere.</summary>
public abstract record GameEvent(int Turn);

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

public sealed record PactSigned(int Turn, Pact Pact) : GameEvent(Turn);

public sealed record PactFulfilled(int Turn, Pact Pact) : GameEvent(Turn);

/// <summary>Public: every court learns about it.</summary>
public sealed record PactBroken(int Turn, Pact Pact, CourtId Breaker) : GameEvent(Turn);

/// <summary>A party was eliminated: no reward and no penalty.</summary>
public sealed record PactCancelled(int Turn, Pact Pact) : GameEvent(Turn);

public sealed record SoldiersDeserted(int Turn, CourtId Court, int Soldiers) : GameEvent(Turn);

public sealed record CourtEliminated(int Turn, CourtId Court) : GameEvent(Turn);
